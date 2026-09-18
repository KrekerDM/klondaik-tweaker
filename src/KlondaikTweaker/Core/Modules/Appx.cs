using System.Runtime.Versioning;
using KlondaikTweaker.Core.Win;
using Windows.Management.Deployment;

namespace KlondaikTweaker.Core.Modules;

public sealed class AppxItem
{
    public string Name { get; set; } = "";
    public string Full { get; set; } = "";
    public string Display { get; set; } = "";
    public string Publisher { get; set; } = "";
    public bool Framework { get; set; }
    public bool System { get; set; }
    public string Group { get; set; } = "other";
}

[SupportedOSPlatform("windows10.0.19041.0")]
public static class Appx
{
    private static PackageManager? _pm;
    private static List<AppxItem>? _cache;
    private static readonly object Gate = new();

    private static readonly string[] Keep =
    [
        "Microsoft.WindowsStore", "Microsoft.DesktopAppInstaller", "Microsoft.VCLibs",
        "Microsoft.UI.Xaml", "Microsoft.NET.Native", "Microsoft.WindowsAppRuntime",
        "Microsoft.SecHealthUI", "Microsoft.WindowsTerminal", "MicrosoftWindows.Client",
        "Microsoft.Windows.Photos", "Microsoft.WindowsNotepad", "Microsoft.WindowsCalculator",
        "Microsoft.ScreenSketch", "Microsoft.Paint", "Microsoft.WindowsCamera"
    ];

    private static PackageManager? Pm
    {
        get
        {
            try { return _pm ??= new PackageManager(); }
            catch { return null; }
        }
    }

    public static bool CanUseApi => Pm is not null;

    public static List<AppxItem> List(bool refresh = false)
    {
        lock (Gate)
        {
            if (!refresh && _cache is not null) return _cache;
            var list = new List<AppxItem>();
            try
            {
                var pm = Pm;
                if (pm is not null)
                {
                    foreach (var p in pm.FindPackagesForUser(""))
                    {
                        try
                        {
                            var isFramework = p.IsFramework;
                            var item = new AppxItem
                            {
                                Name = p.Id.Name,
                                Full = p.Id.FullName,
                                Publisher = p.Id.PublisherId,
                                Framework = isFramework,
                                System = p.SignatureKind == Windows.ApplicationModel.PackageSignatureKind.System
                            };
                            try { item.Display = p.DisplayName; } catch { }
                            if (string.IsNullOrWhiteSpace(item.Display)) item.Display = item.Name;
                            item.Group = Classify(item.Name);
                            list.Add(item);
                        }
                        catch { }
                    }
                }
            }
            catch { }
            _cache = list
                .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(x => x.Display, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            return _cache;
        }
    }

    private static string Classify(string name)
    {
        if (Keep.Any(k => name.StartsWith(k, StringComparison.OrdinalIgnoreCase))) return "essential";
        if (name.Contains("Xbox", StringComparison.OrdinalIgnoreCase) || name.Contains("GamingApp", StringComparison.OrdinalIgnoreCase)) return "xbox";
        if (name.Contains("Bing", StringComparison.OrdinalIgnoreCase) || name.Contains("News", StringComparison.OrdinalIgnoreCase) || name.Contains("Weather", StringComparison.OrdinalIgnoreCase)) return "bing";
        if (name.Contains("Copilot", StringComparison.OrdinalIgnoreCase) || name.Contains("Cortana", StringComparison.OrdinalIgnoreCase) || name.Contains("BingSearch", StringComparison.OrdinalIgnoreCase)) return "ai";
        if (name.Contains("Teams", StringComparison.OrdinalIgnoreCase) || name.Contains("Skype", StringComparison.OrdinalIgnoreCase) || name.Contains("YourPhone", StringComparison.OrdinalIgnoreCase) || name.Contains("People", StringComparison.OrdinalIgnoreCase)) return "social";
        if (name.Contains("Zune", StringComparison.OrdinalIgnoreCase) || name.Contains("Media", StringComparison.OrdinalIgnoreCase) || name.Contains("Clipchamp", StringComparison.OrdinalIgnoreCase) || name.Contains("Spotify", StringComparison.OrdinalIgnoreCase)) return "media";
        if (name.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)) return "microsoft";
        return "other";
    }

    public static bool Installed(string pattern)
    {
        var list = List();
        return list.Any(x => Match(x.Name, pattern));
    }

    private static bool Match(string name, string pattern)
    {
        if (pattern.EndsWith('*')) return name.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        return string.Equals(name, pattern, StringComparison.OrdinalIgnoreCase);
    }

    public static string Remove(string pattern)
    {
        var targets = List().Where(x => Match(x.Name, pattern)).ToList();
        if (targets.Count == 0) return "not installed";
        var pm = Pm;
        var errors = new List<string>();
        foreach (var t in targets)
        {
            var done = false;
            if (pm is not null)
            {
                try
                {
                    var op = pm.RemovePackageAsync(t.Full, RemovalOptions.RemoveForAllUsers);
                    var ev = new ManualResetEventSlim(false);
                    op.Completed = (_, _) => ev.Set();
                    ev.Wait(90000);
                    var r = op.GetResults();
                    done = string.IsNullOrEmpty(r.ErrorText);
                    if (!done) errors.Add(r.ErrorText);
                }
                catch (Exception ex) { errors.Add(ex.Message); }
            }
            if (!done)
            {
                var ps = Sh.Ps($"Get-AppxPackage -AllUsers -Name '{t.Name}' | Remove-AppxPackage -AllUsers -ErrorAction SilentlyContinue");
                done = ps.Ok;
            }
            Sh.Ps($"Get-AppxProvisionedPackage -Online | Where-Object {{ $_.DisplayName -eq '{t.Name}' }} | Remove-AppxProvisionedPackage -Online -AllUsers -ErrorAction SilentlyContinue", 90000);
        }
        lock (Gate) _cache = null;
        return errors.Count == 0 ? "ok" : string.Join("; ", errors.Distinct().Take(2));
    }

    public static void Invalidate() { lock (Gate) _cache = null; }
}
