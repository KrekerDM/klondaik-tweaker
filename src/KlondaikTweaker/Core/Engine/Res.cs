using System.Reflection;

namespace KlondaikTweaker.Core.Engine;

public static class Res
{
    private static readonly Assembly Asm = typeof(Res).Assembly;
    private static readonly string[] Names = Asm.GetManifestResourceNames();

    public static bool Has(string logical) => Names.Contains(logical, StringComparer.OrdinalIgnoreCase);

    public static Stream? Open(string logical)
    {
        var name = Names.FirstOrDefault(x => string.Equals(x, logical, StringComparison.OrdinalIgnoreCase));
        return name is null ? null : Asm.GetManifestResourceStream(name);
    }

    public static string Text(string logical)
    {
        using var s = Open(logical);
        if (s is null) return "";
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    public static IEnumerable<string> All() => Names;
}
