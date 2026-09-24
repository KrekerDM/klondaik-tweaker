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
        ("HKCU", @"Software\Microsoft\Windows\CurrentVersion\Run", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", "run.hkcu"),
        ("HKLM", @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32", "run.hklm"),
        ("HKLM", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32", "run.hklm32")
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
            (Environment.GetFolderPath(Environment.SpecialFolder.Startup), "folder.user"),
            (Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "folder.all")
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
                Source = "task",
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

    public static (bool Ok, string Message, string? Id) Add(string raw)
    {
        var path = (raw ?? "").Trim().Trim('"');
        if (path.Length == 0) return (false, "путь пустой", null);

        try { path = Environment.ExpandEnvironmentVariables(path); } catch { }
        try { path = Path.GetFullPath(path); } catch { return (false, "путь не разобран", null); }

        if (Directory.Exists(path)) return (false, "это папка, а не программа", null);
        if (!File.Exists(path)) return (false, "файл не найден", null);

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext == ".lnk") return AddShortcut(path);
        if (ext is not (".exe" or ".bat" or ".cmd" or ".com"))
            return (false, "можно добавить программу или ярлык, а не " + (ext.Length > 1 ? ext[1..] : "такой файл"), null);

        var key = RunKeys[0];
        var taken = Reg.Values(key.Hive, key.Path).Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = Unique(Path.GetFileNameWithoutExtension(path), taken.Contains);

        try { Reg.Write(key.Hive, key.Path, name, "sz", "\"" + path + "\""); }
        catch (Exception e) { return (false, e.Message, null); }

        return (true, name + " добавлена в автозагрузку", key.Hive + "|" + key.Path + "|" + name);
    }

    private static (bool Ok, string Message, string? Id) AddShortcut(string path)
    {
        var dir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (dir.Length == 0 || !Directory.Exists(dir)) return (false, "папка автозагрузки не найдена", null);

        if (string.Equals(Path.GetDirectoryName(path), dir, StringComparison.OrdinalIgnoreCase))
            return (false, "ярлык уже лежит в автозагрузке", null);

        var name = Unique(Path.GetFileNameWithoutExtension(path), x => File.Exists(Path.Combine(dir, x + ".lnk")));
        var target = Path.Combine(dir, name + ".lnk");

        try { File.Copy(path, target); }
        catch (Exception e) { return (false, e.Message, null); }

        return (true, name + " добавлен в автозагрузку", "file|" + target);
    }

    private static string Unique(string basis, Func<string, bool> taken)
    {
        if (basis.Length == 0) basis = "program";
        if (!taken(basis)) return basis;
        for (var i = 2; i < 100; i++)
        {
            var candidate = basis + " (" + i + ")";
            if (!taken(candidate)) return candidate;
        }
        return basis + " " + Guid.NewGuid().ToString("N")[..6];
    }
}
