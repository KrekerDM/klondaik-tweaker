using System.Xml.Linq;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class GhostDevice
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public bool Removable { get; set; }
}

public static class Ghosts
{
    private static readonly HashSet<string> Removable = new(StringComparer.OrdinalIgnoreCase)
    {
        "USB", "USBDevice", "HIDClass", "Mouse", "Keyboard", "Ports", "Image", "Media",
        "Bluetooth", "Printer", "PrintQueue", "Monitor", "WPD", "AudioEndpoint",
        "Camera", "Sensor", "SmartCardReader", "Biometric"
    };

    private static readonly HashSet<string> Never = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Processor", "Computer", "DiskDrive", "Volume", "SCSIAdapter",
        "HDC", "hdc", "VolumeSnapshot", "SecurityDevices", "FirmwareUpdate", "Firmware"
    };

    public static List<GhostDevice> List()
    {
        var list = new List<GhostDevice>();
        var r = Sh.Run("pnputil.exe", "/enum-devices /disconnected /format xml", 120000);
        if (!r.Ok || r.Out.IndexOf("<PnpUtil", StringComparison.OrdinalIgnoreCase) < 0) return list;

        try
        {
            var doc = XDocument.Parse(r.Out);
            foreach (var node in doc.Descendants("Device"))
            {
                var id = (string?)node.Attribute("InstanceId") ?? "";
                if (id.Length == 0) continue;

                list.Add(new GhostDevice
                {
                    Id = id,
                    Name = (string?)node.Element("DeviceDescription") ?? id,
                    Class = (string?)node.Element("ClassName") ?? ""
                });
            }
        }
        catch { return list; }

        foreach (var device in list)
            device.Removable = Removable.Contains(device.Class) && !Never.Contains(device.Class);

        return list
            .OrderByDescending(x => x.Removable)
            .ThenBy(x => x.Class, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static RepairResult Remove(IEnumerable<string> ids)
    {
        var result = new RepairResult();
        var known = List().ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var id in ids.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!known.TryGetValue(id, out var device))
            {
                result.Skipped++;
                continue;
            }
            if (!device.Removable)
            {
                result.Skipped++;
                if (result.Details.Count < 20) result.Details.Add(device.Name + ": класс не разрешён к удалению");
                continue;
            }

            var r = Sh.Run("pnputil.exe", $"/remove-device \"{id}\"", 60000);
            if (r.Ok) result.Changed++;
            else if (result.Details.Count < 20) result.Details.Add(device.Name + ": " + Trim(r.All));
        }

        result.Message = result.Changed == 0
            ? "ничего не удалено"
            : $"убрано записей: {result.Changed}";
        return result;
    }

    private static string Trim(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 120 ? s[..120] : s;
    }
}
