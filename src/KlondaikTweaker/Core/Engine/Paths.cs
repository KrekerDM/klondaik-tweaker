namespace KlondaikTweaker.Core.Engine;

public static class Paths
{
    public static string Root { get; } = Init();

    private static string Init()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var dir = Path.Combine(baseDir, "KlondaikTweaker");
        try { Directory.CreateDirectory(dir); } catch { }
        return dir;
    }

    public static string Journal => Path.Combine(Root, "journal.json");
    public static string Settings => Path.Combine(Root, "settings.json");
    public static string Backups => EnsureDir(Path.Combine(Root, "backups"));
    public static string Logs => EnsureDir(Path.Combine(Root, "logs"));
    public static string Bench => Path.Combine(Root, "benchmarks.json");
    public static string WebCache => EnsureDir(Path.Combine(Root, "webview"));

    private static string EnsureDir(string p)
    {
        try { Directory.CreateDirectory(p); } catch { }
        return p;
    }
}
