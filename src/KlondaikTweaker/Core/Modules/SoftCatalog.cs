using System.Text.Json;
using System.Text.Json.Serialization;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class SoftItem
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("cat")] public string Cat { get; set; } = "";
    [JsonPropertyName("winget")] public string? Winget { get; set; }
    [JsonPropertyName("url")] public string? Url { get; set; }
    [JsonPropertyName("pick")] public bool Pick { get; set; }
    [JsonPropertyName("ru")] public string Ru { get; set; } = "";
    [JsonPropertyName("en")] public string En { get; set; } = "";
    public bool Installed { get; set; }
}

public sealed class SoftDb
{
    [JsonPropertyName("items")] public List<SoftItem> Items { get; set; } = [];
}

public static class SoftCatalog
{
    private static SoftDb? _db;
    private static HashSet<string>? _installed;
    private static readonly object Gate = new();

    public static SoftDb Db
    {
        get
        {
            lock (Gate)
            {
                if (_db is not null) return _db;
                var json = Res.Text("data/software.json");
                _db = string.IsNullOrWhiteSpace(json) ? new SoftDb() : JsonSerializer.Deserialize<SoftDb>(json, Store.Options) ?? new SoftDb();
                return _db;
            }
        }
    }

    private static bool? _hasWinget;

    public static bool HasWinget(bool refresh = false)
    {
        if (!refresh && _hasWinget is not null) return _hasWinget.Value;
        var r = Sh.Run("winget.exe", "--version", 15000);
        _hasWinget = r.Ok;
        return r.Ok;
    }

    public static bool? WingetKnown => _hasWinget;

    public static HashSet<string>? Peek()
    {
        lock (Gate) return _installed;
    }

    public static List<SoftItem> Quick()
    {
        var known = Peek();
        foreach (var i in Db.Items)
            i.Installed = known is not null && i.Winget is not null && known.Contains(i.Winget);
        return Db.Items;
    }

    public static HashSet<string> Installed(bool refresh = false)
    {
        lock (Gate)
        {
            if (!refresh && _installed is not null) return _installed;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tmp = Path.Combine(Paths.Root, "winget-export.json");
            try
            {
                var r = Sh.Run("winget.exe", $"export -o \"{tmp}\" --accept-source-agreements --disable-interactivity", 180000);
                if (File.Exists(tmp))
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(tmp));
                    if (doc.RootElement.TryGetProperty("Sources", out var sources))
                    {
                        foreach (var s in sources.EnumerateArray())
                        {
                            if (!s.TryGetProperty("Packages", out var pkgs)) continue;
                            foreach (var p in pkgs.EnumerateArray())
                            {
                                if (p.TryGetProperty("PackageIdentifier", out var id)) set.Add(id.GetString() ?? "");
                            }
                        }
                    }
                }
            }
            catch { }
            finally { try { File.Delete(tmp); } catch { } }
            _installed = set;
            return set;
        }
    }

    public static List<SoftItem> List(bool refresh = false)
    {
        var installed = Installed(refresh);
        foreach (var i in Db.Items) i.Installed = i.Winget is not null && installed.Contains(i.Winget);
        return Db.Items;
    }

    public static string Install(string id)
    {
        var item = Db.Items.FirstOrDefault(x => x.Id == id);
        if (item?.Winget is null) return "no winget id";
        var r = Sh.Run("winget.exe", $"install --id {item.Winget} -e --silent --accept-package-agreements --accept-source-agreements --disable-interactivity", 900000);
        lock (Gate) _installed = null;
        if (r.Ok) return "ok";
        if (r.All.Contains("already installed", StringComparison.OrdinalIgnoreCase)) return "ok";
        return r.All.Length > 200 ? r.All[..200] : r.All;
    }

    public static string Uninstall(string id)
    {
        var item = Db.Items.FirstOrDefault(x => x.Id == id);
        if (item?.Winget is null) return "no winget id";
        var r = Sh.Run("winget.exe", $"uninstall --id {item.Winget} -e --silent --accept-source-agreements --disable-interactivity", 900000);
        lock (Gate) _installed = null;
        return r.Ok ? "ok" : (r.All.Length > 200 ? r.All[..200] : r.All);
    }

    public static string UpgradeAll()
    {
        var r = Sh.Run("winget.exe", "upgrade --all --silent --accept-package-agreements --accept-source-agreements --disable-interactivity", 1800000);
        lock (Gate) _installed = null;
        return r.Ok ? "ok" : (r.All.Length > 300 ? r.All[..300] : r.All);
    }
}
