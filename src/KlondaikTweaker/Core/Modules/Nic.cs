using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Model;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class NicOption
{
    public string Value { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class NicParam
{
    public string Name { get; set; } = "";
    public string Desc { get; set; } = "";
    public string Type { get; set; } = "edit";
    public string Current { get; set; } = "";
    public string Default { get; set; } = "";
    public bool Edited { get; set; }
    public bool Risky { get; set; }
    public string? Min { get; set; }
    public string? Max { get; set; }
    public List<NicOption> Options { get; set; } = [];
}

public sealed class NicAdapter
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Service { get; set; } = "";
}

public static class Nic
{
    private const string ClassRoot = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

    private static readonly HashSet<string> Risky = new(StringComparer.OrdinalIgnoreCase)
    {
        "*SpeedDuplex", "SpeedDuplex", "*JumboPacket", "NetworkAddress", "*PriorityVLANTag"
    };

    private static readonly string[] LatencyOff =
    [
        "*InterruptModeration", "*FlowControl", "*EEE", "EnableGreenEthernet", "AdvancedEEE", "EnableSavePowerNow"
    ];

    public static List<NicAdapter> Adapters()
    {
        var list = new List<NicAdapter>();

        foreach (var key in Reg.SubKeys("HKLM", ClassRoot))
        {
            if (key.Length != 4 || !key.All(char.IsDigit)) continue;
            var path = $@"{ClassRoot}\{key}";

            var name = Reg.Read("HKLM", path, "DriverDesc").Value;
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (!Reg.KeyExists("HKLM", $@"{path}\Ndi\Params")) continue;

            list.Add(new NicAdapter
            {
                Id = key,
                Name = name.Trim(),
                Service = Reg.Read("HKLM", path, "NetCfgInstanceId").Value
            });
        }

        return list.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public static List<NicParam> Params(string id)
    {
        var list = new List<NicParam>();
        if (!Valid(id)) return list;

        var path = $@"{ClassRoot}\{id}";
        var paramsPath = $@"{path}\Ndi\Params";

        foreach (var name in Reg.SubKeys("HKLM", paramsPath))
        {
            var p = $@"{paramsPath}\{name}";

            var desc = Reg.Read("HKLM", p, "ParamDesc").Value.Trim();
            if (desc.Length == 0) continue;

            var item = new NicParam
            {
                Name = name,
                Desc = desc,
                Type = Reg.Read("HKLM", p, "type").Value.Trim().ToLowerInvariant(),
                Default = Reg.Read("HKLM", p, "default").Value.Trim(),
                Risky = Risky.Contains(name)
            };

            if (item.Type.Length == 0) item.Type = "edit";

            var current = Reg.Read("HKLM", path, name);
            item.Current = current.Exists ? current.Value.Trim() : item.Default;
            item.Edited = current.Exists && !string.Equals(item.Current, item.Default, StringComparison.OrdinalIgnoreCase);

            if (item.Type == "enum")
            {
                foreach (var (value, text) in Reg.Values("HKLM", $@"{p}\Enum"))
                {
                    var label = text.Trim();
                    item.Options.Add(new NicOption { Value = value, Text = label.Length > 0 ? label : value });
                }
                item.Options = item.Options.OrderBy(x => Num(x.Value)).ToList();
            }
            else
            {
                item.Min = Nullable(Reg.Read("HKLM", p, "min").Value);
                item.Max = Nullable(Reg.Read("HKLM", p, "max").Value);
            }

            list.Add(item);
        }

        return list
            .OrderByDescending(x => x.Edited)
            .ThenBy(x => x.Desc, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static RepairResult Set(string id, string name, string value)
    {
        var result = new RepairResult();
        if (!Valid(id) || !ValidName(name))
        {
            result.Ok = false;
            result.Message = Texts.Pick("недопустимый адаптер или параметр", "invalid adapter or setting");
            return result;
        }

        var known = Params(id).FirstOrDefault(x => x.Name.Equals(name, StringComparison.Ordinal));
        if (known is null)
        {
            result.Ok = false;
            result.Message = Texts.Pick("у этого адаптера нет такого параметра", "this adapter has no such setting");
            return result;
        }

        if (known.Type == "enum" && known.Options.Count > 0 &&
            !known.Options.Any(x => x.Value.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            result.Ok = false;
            result.Message = Texts.Pick("значение не из списка допустимых", "the value is not one of the allowed options");
            return result;
        }

        var path = $@"{ClassRoot}\{id}";
        var entry = new JournalEntry { TweakId = "nic." + id + "." + name, Title = known.Desc, Group = "nic" };

        try
        {
            var prev = Reg.Read("HKLM", path, name);
            entry.Items.Add(new JournalItem
            {
                Kind = "reg",
                Target = "HKLM\\" + path,
                Name = name,
                Existed = prev.Exists,
                PrevType = prev.Exists ? prev.Kind : "sz",
                PrevValue = prev.Exists ? prev.Value : null
            });
            Reg.Write("HKLM", path, name, "sz", value);
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Message = Trim(ex.Message);
            return result;
        }

        Journal.Add(entry);
        result.Changed = 1;
        result.Message = Texts.Pick("параметр записан, нужен перезапуск адаптера", "setting written, the adapter needs a restart");
        return result;
    }

    public static RepairResult Preset(string id, string preset)
    {
        var result = new RepairResult();
        if (!Valid(id))
        {
            result.Ok = false;
            result.Message = Texts.Pick("адаптер не найден", "adapter not found");
            return result;
        }

        var all = Params(id);
        var targets = preset == "latency"
            ? all.Where(x => LatencyOff.Contains(x.Name, StringComparer.OrdinalIgnoreCase)).ToList()
            : all.Where(x => x.Edited && !x.Risky).ToList();

        foreach (var p in targets)
        {
            var value = preset == "latency" ? Off(p) : p.Default;
            if (value is null) { result.Skipped++; continue; }
            if (string.Equals(p.Current, value, StringComparison.OrdinalIgnoreCase)) { result.Skipped++; continue; }

            var one = Set(id, p.Name, value);
            if (one.Ok) result.Changed++;
            else result.Details.Add(p.Desc + ": " + one.Message);
        }

        result.Message = result.Changed == 0
            ? Texts.Pick("менять нечего", "nothing to change")
            : Texts.Pick($"изменено параметров: {result.Changed}, нужен перезапуск адаптера", $"settings changed: {result.Changed}, the adapter needs a restart");
        return result;
    }

    private static string? Off(NicParam p)
    {
        if (p.Type != "enum") return null;
        var zero = p.Options.FirstOrDefault(x => x.Value.Trim() == "0");
        return zero?.Value;
    }

    public static RepairResult Restart(string id)
    {
        var adapter = Adapters().FirstOrDefault(x => x.Id == id);
        if (adapter is null) return new RepairResult { Ok = false, Message = Texts.Pick("адаптер не найден", "adapter not found") };

        var safe = adapter.Name.Replace("'", "''");
        var r = Sh.Ps($"Get-NetAdapter -InterfaceDescription '{safe}' -ErrorAction Stop | Restart-NetAdapter -Confirm:$false", 120000);
        return r.Ok
            ? new RepairResult { Changed = 1, Message = Texts.Pick("адаптер перезапущен, параметры вступили в силу", "adapter restarted, the settings are in effect") }
            : new RepairResult { Ok = false, Message = Trim(r.All) };
    }

    private static bool Valid(string id) => id.Length == 4 && id.All(char.IsDigit);

    private static bool ValidName(string name) =>
        name.Length is > 0 and <= 64 && name.All(c => char.IsLetterOrDigit(c) || c == '*' || c == '_' || c == '.');

    private static int Num(string s) => int.TryParse(s, out var n) ? n : int.MaxValue;

    private static string? Nullable(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string Trim(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 200 ? s[..200] : s;
    }
}
