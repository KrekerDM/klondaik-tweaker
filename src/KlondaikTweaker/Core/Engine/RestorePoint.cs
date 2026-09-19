using System.Globalization;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Engine;

public sealed record RestoreEntry(int Seq, string Desc, string Time);

public sealed record RestoreResult(bool Ok, string Message);

public static class RestorePoint
{
    private const string SrKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
    private const string SrPolicy = @"SOFTWARE\Policies\Microsoft\Windows NT\SystemRestore";

    public static bool Enabled()
    {
        var policy = Reg.Read("HKLM", SrPolicy, "DisableSR");
        if (policy.Exists && policy.Value == "1") return false;
        var clients = Reg.Values("HKLM", @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SPP\Clients");
        return clients.Length > 0;
    }

    public static string Enable()
    {
        try
        {
            Reg.Write("HKLM", SrKey, "SystemRestorePointCreationFrequency", "dword", "0");
            Reg.DeleteValue("HKLM", SrPolicy, "DisableSR");
            Reg.DeleteValue("HKLM", SrPolicy, "DisableConfig");
        }
        catch { }

        var root = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? "C:\\";
        var drive = root.Length >= 2 ? root[..2] : "C:";
        var r = Sh.Ps($"Enable-ComputerRestore -Drive '{drive}\\' -ErrorAction Stop", 120000);
        if (!r.Ok) return Short(r.All);
        Sh.Run("vssadmin.exe", $"resize shadowstorage /for={drive} /on={drive} /maxsize=5%", 30000);
        return "ok";
    }

    public static RestoreResult Create(string description)
    {
        try { Reg.Write("HKLM", SrKey, "SystemRestorePointCreationFrequency", "dword", "0"); }
        catch { }

        var safe = description.Replace("'", "").Replace("\"", "");
        var r = Sh.Ps($"Checkpoint-Computer -Description '{safe}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop", 300000);
        if (r.Ok) return new RestoreResult(true, "ok");

        var text = r.All;
        if (text.Contains("disabled", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("отключ", StringComparison.OrdinalIgnoreCase))
            return new RestoreResult(false, "disabled");
        return new RestoreResult(false, Short(text));
    }

    public static List<RestoreEntry> List()
    {
        var list = new List<RestoreEntry>();
        var r = Sh.Ps("Get-ComputerRestorePoint | Select-Object -Last 15 | ForEach-Object { \"{0}`t{1}`t{2}\" -f $_.SequenceNumber, $_.Description, $_.CreationTime }", 120000);
        if (!r.Ok) return list;

        foreach (var line in r.Out.Split('\n'))
        {
            var parts = line.Split('\t');
            if (parts.Length < 3) continue;
            if (!int.TryParse(parts[0].Trim(), out var seq)) continue;
            list.Add(new RestoreEntry(seq, parts[1].Trim(), FormatTime(parts[2].Trim())));
        }
        return list.OrderByDescending(x => x.Seq).ToList();
    }

    private static string FormatTime(string raw)
    {
        if (raw.Length >= 14 && raw.All(char.IsDigit))
            return $"{raw[..4]}-{raw.Substring(4, 2)}-{raw.Substring(6, 2)} {raw.Substring(8, 2)}:{raw.Substring(10, 2)}";
        if (DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out var parsed))
            return parsed.ToString("yyyy-MM-dd HH:mm");
        return raw;
    }

    private static string Short(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 160 ? s[..160] : s;
    }

    public static void OpenUi() => Sh.Run("rstrui.exe", "", 2000);
}
