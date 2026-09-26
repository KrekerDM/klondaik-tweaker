namespace KlondaikTweaker.Core.Engine;

public static class Texts
{
    private static readonly HashSet<string> LatinText = new(StringComparer.OrdinalIgnoreCase)
    {
        "en", "de", "pl", "es", "fr"
    };

    public static bool English => LatinText.Contains(Settings.Data.Lang);

    public static string Lang => English ? "en" : "ru";

    public static string Pick(string ru, string en) => English ? en : ru;
}
