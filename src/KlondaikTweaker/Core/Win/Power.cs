namespace KlondaikTweaker.Core.Win;

public static class Power
{
    public static string? ActiveScheme()
    {
        var r = Sh.Run("powercfg.exe", "/getactivescheme", 10000);
        if (!r.Ok) return null;
        var parts = r.Out.Split(':', 2);
        if (parts.Length < 2) return null;
        var tail = parts[1].Trim();
        var open = tail.IndexOf('(');
        var guid = open > 0 ? tail[..open].Trim() : tail;
        return Guid.TryParse(guid, out _) ? guid : null;
    }
}
