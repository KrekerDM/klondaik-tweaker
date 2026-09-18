using System.Text;

namespace KlondaikTweaker.Core.Win;

public static class HostsFile
{
    private const string Begin = "# klondaik-begin";
    private const string End = "# klondaik-end";

    public static string Path => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    private static List<string> Read()
    {
        try { return File.Exists(Path) ? File.ReadAllLines(Path).ToList() : []; }
        catch { return []; }
    }

    public static HashSet<string> Current()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool inside = false;
        foreach (var l in Read())
        {
            if (l.StartsWith(Begin, StringComparison.OrdinalIgnoreCase)) { inside = true; continue; }
            if (l.StartsWith(End, StringComparison.OrdinalIgnoreCase)) { inside = false; continue; }
            if (!inside) continue;
            var parts = l.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) set.Add(parts[1]);
        }
        return set;
    }

    private static void Write(IEnumerable<string> hosts)
    {
        var lines = Read();
        var b = lines.FindIndex(x => x.StartsWith(Begin, StringComparison.OrdinalIgnoreCase));
        var e = lines.FindIndex(x => x.StartsWith(End, StringComparison.OrdinalIgnoreCase));
        if (b >= 0 && e > b) lines.RemoveRange(b, e - b + 1);
        var list = hosts.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        if (list.Count > 0)
        {
            lines.Add(Begin);
            foreach (var h in list) lines.Add("0.0.0.0 " + h);
            lines.Add(End);
        }
        var tmp = Path + ".kl";
        File.WriteAllLines(tmp, lines, new UTF8Encoding(false));
        File.Copy(tmp, Path, true);
        File.Delete(tmp);
        Sh.Run("ipconfig.exe", "/flushdns", 15000);
    }

    public static void Block(IEnumerable<string> hosts)
    {
        var set = Current();
        foreach (var h in hosts) set.Add(h.Trim());
        Write(set);
    }

    public static void Unblock(IEnumerable<string> hosts)
    {
        var set = Current();
        foreach (var h in hosts) set.Remove(h.Trim());
        Write(set);
    }

    public static void ClearAll() => Write([]);
}
