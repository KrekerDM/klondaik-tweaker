using System.Text.RegularExpressions;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Model;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class PowerScheme
{
    public string Guid { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; }
}

public static class PowerPlans
{
    private const string SettingsRoot = @"SYSTEM\CurrentControlSet\Control\Power\PowerSettings";
    private const int Hidden = 1;
    private const int Shown = 2;

    private static readonly Regex Line =
        new(@"([0-9a-fA-F-]{36})\s*\(([^)]*)\)", RegexOptions.Compiled);

    public static string Folder
    {
        get
        {
            var dir = Path.Combine(Paths.Root, "power");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    public static List<PowerScheme> Schemes()
    {
        var list = new List<PowerScheme>();
        var r = Sh.Run("powercfg.exe", "/list", 30000);
        if (!r.Ok) return list;

        foreach (var raw in r.All.Split('\n'))
        {
            var m = Line.Match(raw);
            if (!m.Success) continue;
            list.Add(new PowerScheme
            {
                Guid = m.Groups[1].Value.ToLowerInvariant(),
                Name = m.Groups[2].Value.Trim(),
                Active = raw.Contains('*')
            });
        }
        return list;
    }

    public static List<string> Files() =>
        Directory.EnumerateFiles(Folder, "*.pow")
            .Select(Path.GetFileName)
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    public static RepairResult Activate(string guid)
    {
        if (!ValidGuid(guid)) return Bad(Texts.Pick("неверный идентификатор схемы", "invalid scheme id"));
        var before = Power.ActiveScheme();
        var r = Sh.Run("powercfg.exe", $"/setactive {guid}", 30000);
        if (!r.Ok) return Bad(Trim(r.All));

        if (before is not null && !before.Equals(guid, StringComparison.OrdinalIgnoreCase))
        {
            var entry = new JournalEntry { TweakId = "power.scheme", Title = Texts.Pick("Схема электропитания", "Power scheme"), Group = "power" };
            entry.Items.Add(new JournalItem
            {
                Kind = "cmd",
                Target = "powercfg.exe",
                PrevValue = $"/setactive {guid}",
                Revert = $"/setactive {before}"
            });
            Journal.Add(entry);
        }

        return new RepairResult { Changed = 1, Message = Texts.Pick("схема включена", "scheme activated") };
    }

    public static RepairResult Delete(string guid)
    {
        if (!ValidGuid(guid)) return Bad(Texts.Pick("неверный идентификатор схемы", "invalid scheme id"));
        if (string.Equals(Power.ActiveScheme(), guid, StringComparison.OrdinalIgnoreCase))
            return Bad(Texts.Pick("нельзя удалить схему, которая сейчас активна", "the active scheme cannot be deleted"));

        var r = Sh.Run("powercfg.exe", $"/delete {guid}", 30000);
        return r.Ok
            ? new RepairResult { Changed = 1, Message = Texts.Pick("схема удалена", "scheme deleted") }
            : Bad(Trim(r.All));
    }

    public static RepairResult Export(string guid)
    {
        if (!ValidGuid(guid)) return Bad(Texts.Pick("неверный идентификатор схемы", "invalid scheme id"));

        var scheme = Schemes().FirstOrDefault(x => x.Guid.Equals(guid, StringComparison.OrdinalIgnoreCase));
        var name = Safe(scheme?.Name ?? guid);
        var file = Path.Combine(Folder, name + ".pow");

        var r = Sh.Run("powercfg.exe", $"/export \"{file}\" {guid}", 60000);
        return r.Ok
            ? new RepairResult { Changed = 1, Message = Texts.Pick("сохранено в ", "saved to ") + Path.GetFileName(file) }
            : Bad(Trim(r.All));
    }

    public static RepairResult Import(string fileName)
    {
        if (fileName.Contains("..") || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return Bad(Texts.Pick("недопустимое имя файла", "invalid file name"));

        var file = Path.Combine(Folder, fileName);
        if (!File.Exists(file)) return Bad(Texts.Pick("файл не найден в папке схем", "the file is not in the schemes folder"));

        var r = Sh.Run("powercfg.exe", $"/import \"{file}\"", 60000);
        return r.Ok
            ? new RepairResult { Changed = 1, Message = Texts.Pick("схема добавлена в список", "scheme added to the list") }
            : Bad(Trim(r.All));
    }

    public static int HiddenCount()
    {
        var count = 0;
        foreach (var (_, _, attributes) in Settings())
            if (attributes == Hidden) count++;
        return count;
    }

    public static RepairResult Unhide()
    {
        var result = new RepairResult();
        var entry = new JournalEntry
        {
            TweakId = "power.unhide",
            Title = Texts.Pick("Скрытые параметры электропитания", "Hidden power settings"),
            Group = "power"
        };

        foreach (var (subgroup, setting, attributes) in Settings())
        {
            if (attributes != Hidden) continue;
            var path = $@"{SettingsRoot}\{subgroup}\{setting}";
            try
            {
                entry.Items.Add(new JournalItem
                {
                    Kind = "reg",
                    Target = "HKLM\\" + path,
                    Name = "Attributes",
                    Existed = true,
                    PrevType = "dword",
                    PrevValue = Hidden.ToString()
                });
                Reg.Write("HKLM", path, "Attributes", "dword", Shown.ToString());
                result.Changed++;
            }
            catch (Exception ex)
            {
                if (result.Details.Count < 20) result.Details.Add(setting + ": " + Trim(ex.Message));
            }
        }

        if (entry.Items.Count > 0) Journal.Add(entry);
        result.Message = result.Changed == 0
            ? Texts.Pick("скрытых параметров не осталось", "no hidden settings are left")
            : Texts.Pick($"открыто параметров: {result.Changed}", $"settings revealed: {result.Changed}");
        return result;
    }

    private static IEnumerable<(string Subgroup, string Setting, int Attributes)> Settings()
    {
        foreach (var subgroup in Reg.SubKeys("HKLM", SettingsRoot))
        {
            foreach (var setting in Reg.SubKeys("HKLM", $@"{SettingsRoot}\{subgroup}"))
            {
                var value = Reg.Read("HKLM", $@"{SettingsRoot}\{subgroup}\{setting}", "Attributes");
                if (!value.Exists || !int.TryParse(value.Value, out var attributes)) continue;
                yield return (subgroup, setting, attributes);
            }
        }
    }

    private static bool ValidGuid(string guid) => Guid.TryParse(guid, out _);

    private static string Safe(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '-');
        name = name.Trim();
        return name.Length == 0 ? "scheme" : name;
    }

    private static RepairResult Bad(string message) => new() { Ok = false, Message = message };

    private static string Trim(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 200 ? s[..200] : s;
    }
}
