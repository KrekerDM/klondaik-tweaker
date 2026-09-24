using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class NvidiaState
{
    public bool Present { get; set; }
    public string Gpu { get; set; } = "";
    public string Version { get; set; } = "";
    public bool Ready { get; set; }
    public List<string> Files { get; set; } = [];
}

public static class Nvidia
{
    private const string Exe = "nvidiaProfileInspector.exe";

    public static string Folder
    {
        get
        {
            var dir = Path.Combine(Paths.Root, "nvidia");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private static string ToolPath => Path.Combine(Folder, Exe);

    public static NvidiaState State()
    {
        var facts = Env.Facts;
        return new NvidiaState
        {
            Present = facts.Nvidia,
            Gpu = facts.Gpus.FirstOrDefault(x => x.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase)) ?? "",
            Version = Res.Text("vendor/version.txt").Trim(),
            Ready = File.Exists(ToolPath),
            Files = Files()
        };
    }

    public static List<string> Files() =>
        Directory.EnumerateFiles(Folder, "*.nip")
            .Select(Path.GetFileName)
            .Where(x => x is not null)
            .Select(x => x!)
            .OrderByDescending(x => x, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private static bool Ensure()
    {
        if (File.Exists(ToolPath)) return true;
        try
        {
            var bytes = Res.Bytes("vendor/" + Exe);
            if (bytes is null || bytes.Length < 1024) return false;
            File.WriteAllBytes(ToolPath, bytes);

            var license = Res.Text("vendor/nvidiaProfileInspector-LICENSE.txt");
            if (license.Length > 0)
                File.WriteAllText(Path.Combine(Folder, "nvidiaProfileInspector-LICENSE.txt"), license);

            return File.Exists(ToolPath);
        }
        catch { return false; }
    }

    public static RepairResult Export()
    {
        if (!Ensure()) return Bad("не удалось распаковать NVIDIA Profile Inspector");

        var before = Directory.GetFiles(Folder, "*.nip").ToHashSet(StringComparer.OrdinalIgnoreCase);
        var r = Sh.Run(ToolPath, "-exportCustomized", 180000);
        if (!r.Ok) return Bad(Trim(r.All));

        var fresh = Directory.GetFiles(Folder, "*.nip").FirstOrDefault(x => !before.Contains(x));
        return fresh is null
            ? new RepairResult { Message = "изменённых профилей не нашлось, сохранять нечего" }
            : new RepairResult { Changed = 1, Message = "сохранено: " + Path.GetFileName(fresh) };
    }

    public static RepairResult Import(string fileName)
    {
        if (fileName.Contains("..") || fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return Bad("недопустимое имя файла");
        if (!fileName.EndsWith(".nip", StringComparison.OrdinalIgnoreCase))
            return Bad("нужен файл .nip");

        var file = Path.Combine(Folder, fileName);
        if (!File.Exists(file)) return Bad("файл не найден в папке профилей");
        if (!Ensure()) return Bad("не удалось распаковать NVIDIA Profile Inspector");

        var r = Sh.Run(ToolPath, $"-silentImport \"{file}\"", 180000);
        return r.Ok
            ? new RepairResult { Changed = 1, Message = "профиль применён к драйверу" }
            : Bad(Trim(r.All));
    }

    public static RepairResult Open()
    {
        if (!Ensure()) return Bad("не удалось распаковать NVIDIA Profile Inspector");
        Sh.OpenExternal(ToolPath);
        return new RepairResult { Message = "окно Profile Inspector открыто" };
    }

    private static RepairResult Bad(string message) => new() { Ok = false, Message = message };

    private static string Trim(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 200 ? s[..200] : s;
    }
}
