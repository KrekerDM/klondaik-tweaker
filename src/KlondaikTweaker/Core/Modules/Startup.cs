using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class StartupItem
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
    public string Source { get; set; } = "";
    public string Kind { get; set; } = "reg";
    public bool Enabled { get; set; } = true;
    public string? Publisher { get; set; }
}

public static class Startup
{
    private static readonly (string Hive, string Path, string Approved, string Label)[] RunKeys =
    [
        ("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "HKCU Run"),
        ("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32", "HKLM Run"),
        ("HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32", "HKLM Run x86")
    ];

    public static List<StartupItem> List()
    {
        var res = new List<StartupItem>();

        foreach (var k in RunKeys)
        {
            foreach (var (name, value) in Reg.Values(k.Hive, k.Path))
            {
                if (name.Length == 0) continue;
                res.Add(new StartupItem
                {
                    Id = k.Hive + "|" + k.Path + "|" + name,
                    Name = name,
                    Command = value,
                    Source = k.Label,
                    Kind = "reg",
                    Enabled = IsApproved(k.Hive, k.Approved, name)
                });
            }
        }

        foreach (var (dir, label) in new[]
        {
            (Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Startup (user)"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Startup (all users)")
        })
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".ini") continue;
                    res.Add(new StartupItem
                    {
                        Id = "file|" + f,
                        Name = Path.GetFileNameWithoutExtension(f),
                        Command = f,
                        Source = label,
                        Kind = "file",
                        Enabled = ext != ".disabled"
                    });
                }
            }
            catch { }
        }

        foreach (var t in Tasks.Startup())
        {
            res.Add(new StartupItem
            {
                Id = "task|" + t.Path,
                Name = t.Name,
                Command = t.Action,
                Source = "Планировщик",
                Kind = "task",
                Enabled = t.Enabled,
                Publisher = t.Author
            });
        }

        return res.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static bool IsApproved(string hive, string approvedPath, string name)
    {
        var r = Reg.Read(hive == "HKLM" ? "HKLM" : "HKCU", approvedPath, name);
        if (!r.Exists) return true;
        try
        {
            var bytes = Reg.ParseHex(r.Value);
            return bytes.Length == 0 || (bytes[0] & 0x01) == 0;
        }
        catch { return true; }
    }

    public static bool SetEnabled(string id, bool enabled)
    {
        var parts = id.Split('|');
        try
        {
            if (parts[0] == "task") return Tasks.SetEnabled(parts[1], enabled);
            if (parts[0] == "file")
            {
                var p = parts[1];
                if (enabled && p.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                    File.Move(p, p[..^9], true);
                else if (!enabled && !p.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                    File.Move(p, p + ".disabled", true);
                return true;
            }
            if (parts.Length < 3) return false;
            var hive = parts[0];
            var path = parts[1];
            var name = parts[2];
            var key = RunKeys.FirstOrDefault(x => x.Hive == hive && x.Path == path);
            if (key.Approved is null) return false;
            var approvedHive = hive == "HKLM" ? "HKLM" : "HKCU";
            var approvedPath = hive == "HKLM" ? @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run" : key.Approved;
            var payload = new byte[12];
            payload[0] = (byte)(enabled ? 0x02 : 0x03);
            if (!enabled)
            {
                var ticks = BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc());
                Array.Copy(ticks, 0, payload, 4, 8);
            }
            Reg.Write(approvedHive, approvedPath, name, "binary", Convert.ToHexString(payload));
            return true;
        }
        catch { return false; }
    }

    public static bool Delete(string id)
    {
        var parts = id.Split('|');
        try
        {
            if (parts[0] == "task") return Tasks.SetEnabled(parts[1], false);
            if (parts[0] == "file")
            {
                File.Delete(parts[1]);
                return true;
            }
            if (parts.Length < 3) return false;
            Reg.DeleteValue(parts[0], parts[1], parts[2]);
            return true;
        }
        catch { return false; }
    }
}
