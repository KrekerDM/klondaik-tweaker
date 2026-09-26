using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Model;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class IrqDevice
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "other";
    public string Service { get; set; } = "";
    public int Policy { get; set; }
    public ulong Mask { get; set; }
    public string MaskHex { get; set; } = "";
    public List<int> Threads { get; set; } = [];
    public int Priority { get; set; }
    public bool Bound { get; set; }
}

public static class Irq
{
    private const string EnumRoot = @"SYSTEM\CurrentControlSet\Enum\PCI";
    private const string Affinity = @"Device Parameters\Interrupt Management\Affinity Policy";

    private const int PolicySpecified = 4;

    private static readonly Dictionary<string, string> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["{4d36e968-e325-11ce-bfc1-08002be10318}"] = "gpu",
        ["{4d36e972-e325-11ce-bfc1-08002be10318}"] = "net",
        ["{36fc9e60-c465-11cf-8056-444553540000}"] = "usb",
        ["{4d36e97b-e325-11ce-bfc1-08002be10318}"] = "storage",
        ["{4d36e96a-e325-11ce-bfc1-08002be10318}"] = "storage",
        ["{4d36e96c-e325-11ce-bfc1-08002be10318}"] = "audio"
    };

    public static List<IrqDevice> Devices()
    {
        var list = new List<IrqDevice>();

        foreach (var hardware in Reg.SubKeys("HKLM", EnumRoot))
        {
            foreach (var instance in Reg.SubKeys("HKLM", $@"{EnumRoot}\{hardware}"))
            {
                var path = $@"{EnumRoot}\{hardware}\{instance}";

                var classGuid = Reg.Read("HKLM", path, "ClassGUID").Value;
                if (!Kinds.TryGetValue(classGuid.Trim(), out var kind)) continue;

                var name = Reg.Read("HKLM", path, "FriendlyName").Value;
                if (string.IsNullOrWhiteSpace(name)) name = Reg.Read("HKLM", path, "DeviceDesc").Value;
                name = Pretty(name);
                if (name.Length == 0) continue;

                var device = new IrqDevice
                {
                    Id = $@"PCI\{hardware}\{instance}",
                    Name = name,
                    Kind = kind,
                    Service = Reg.Read("HKLM", path, "Service").Value
                };

                var policyPath = $@"{path}\{Affinity}";
                var policy = Reg.Read("HKLM", policyPath, "DevicePolicy");
                if (policy.Exists && int.TryParse(policy.Value, out var p)) device.Policy = p;

                var priority = Reg.Read("HKLM", policyPath, "DevicePriority");
                if (priority.Exists && int.TryParse(priority.Value, out var pr)) device.Priority = pr;

                var mask = Reg.Read("HKLM", policyPath, "AssignmentSetOverride");
                if (mask.Exists) device.Mask = FromHex(mask.Value);

                device.Bound = device.Policy == PolicySpecified && device.Mask != 0;
                device.MaskHex = device.Mask == 0 ? "" : "0x" + device.Mask.ToString("X");
                for (var bit = 0; bit < 64; bit++)
                    if ((device.Mask & (1UL << bit)) != 0) device.Threads.Add(bit);
                list.Add(device);
            }
        }

        return list
            .OrderBy(x => Order(x.Kind))
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private static int Order(string kind) => kind switch
    {
        "gpu" => 0,
        "usb" => 1,
        "net" => 2,
        "storage" => 3,
        "audio" => 4,
        _ => 5
    };

    private static string Pretty(string raw)
    {
        raw = raw.Trim();
        var semi = raw.LastIndexOf(';');
        if (raw.StartsWith('@') && semi >= 0 && semi + 1 < raw.Length) raw = raw[(semi + 1)..];
        return raw.Trim();
    }

    private static ulong FromHex(string hex)
    {
        hex = hex.Trim();
        if (hex.Length == 0 || hex.Length % 2 != 0) return 0;
        ulong value = 0;
        for (var i = 0; i < hex.Length && i < 16; i += 2)
        {
            if (!byte.TryParse(hex.AsSpan(i, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)) return 0;
            value |= (ulong)b << (i / 2 * 8);
        }
        return value;
    }

    private static string ToHex(ulong mask)
    {
        var bytes = BitConverter.GetBytes(mask);
        return string.Concat(bytes.Select(b => b.ToString("X2")));
    }

    public static RepairResult Bind(string id, IEnumerable<int> threads, bool highPriority)
    {
        var result = new RepairResult();

        ulong mask = 0;
        foreach (var t in threads)
        {
            if (t < 0 || t > 63)
            {
                result.Ok = false;
                result.Message = Texts.Pick("номер потока вне допустимого диапазона", "the thread number is out of range");
                return result;
            }
            mask |= 1UL << t;
        }

        var device = Devices().FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (device is null)
        {
            result.Ok = false;
            result.Message = Texts.Pick("устройство не найдено", "device not found");
            return result;
        }

        var cpuCount = Cpu.Count();
        var allowed = cpuCount >= 64 ? ulong.MaxValue : (1UL << cpuCount) - 1;
        if (mask == 0 || (mask & ~allowed) != 0)
        {
            result.Ok = false;
            result.Message = Texts.Pick("маска указывает на потоки, которых нет в этом процессоре", "the mask points at threads this processor does not have");
            return result;
        }

        var path = $@"SYSTEM\CurrentControlSet\Enum\{id}\{Affinity}";
        var entry = new JournalEntry { TweakId = "irq." + id, Title = device.Name, Group = "irq" };

        try
        {
            if (!Reg.KeyExists("HKLM", path))
                entry.Items.Add(new JournalItem { Kind = "regnewkey", Target = "HKLM\\" + path });

            Remember(entry, path, "DevicePolicy");
            Remember(entry, path, "AssignmentSetOverride");
            Remember(entry, path, "DevicePriority");

            Reg.Write("HKLM", path, "DevicePolicy", "dword", PolicySpecified.ToString());
            Reg.Write("HKLM", path, "AssignmentSetOverride", "binary", ToHex(mask));
            Reg.Write("HKLM", path, "DevicePriority", "dword", highPriority ? "3" : "0");
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Message = Trim(ex.Message);
            return result;
        }

        if (entry.Items.Count > 0) Journal.Add(entry);
        result.Changed = 1;
        result.Message = Texts.Pick("прерывания привязаны, изменение вступит в силу после перезагрузки", "interrupts pinned, the change takes effect after a restart");
        return result;
    }

    public static RepairResult Reset(string id)
    {
        var result = new RepairResult();
        var path = $@"SYSTEM\CurrentControlSet\Enum\{id}\{Affinity}";

        try
        {
            if (!Reg.KeyExists("HKLM", path))
            {
                result.Message = Texts.Pick("у устройства и так нет привязки", "the device has no affinity set anyway");
                return result;
            }

            var device = Devices().FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            var entry = new JournalEntry
            {
                TweakId = "irq.reset." + id,
                Title = device?.Name ?? id,
                Group = "irq"
            };
            Remember(entry, path, "DevicePolicy");
            Remember(entry, path, "AssignmentSetOverride");
            Remember(entry, path, "DevicePriority");

            Reg.DeleteValue("HKLM", path, "DevicePolicy");
            Reg.DeleteValue("HKLM", path, "AssignmentSetOverride");
            Reg.DeleteValue("HKLM", path, "DevicePriority");

            if (entry.Items.Count > 0) Journal.Add(entry);
            result.Changed = 1;
            result.Message = Texts.Pick("привязка снята, изменение вступит в силу после перезагрузки", "affinity cleared, the change takes effect after a restart");
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Message = Trim(ex.Message);
        }
        return result;
    }

    private static void Remember(JournalEntry entry, string path, string name)
    {
        var prev = Reg.Read("HKLM", path, name);
        entry.Items.Add(new JournalItem
        {
            Kind = "reg",
            Target = "HKLM\\" + path,
            Name = name,
            Existed = prev.Exists,
            PrevType = prev.Exists ? prev.Kind : "dword",
            PrevValue = prev.Exists ? prev.Value : null
        });
    }

    private static string Trim(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 200 ? s[..200] : s;
    }
}
