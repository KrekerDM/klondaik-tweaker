using System.Text.Json;
using System.Text.Json.Serialization;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Model;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class FeatureMeta
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("ru")] public string Ru { get; set; } = "";
    [JsonPropertyName("en")] public string En { get; set; } = "";
    [JsonPropertyName("dru")] public string DRu { get; set; } = "";
    [JsonPropertyName("den")] public string DEn { get; set; } = "";
    [JsonPropertyName("rec")] public string Rec { get; set; } = "keep";
    [JsonPropertyName("group")] public string Group { get; set; } = "other";
}

public sealed class FeatureDb
{
    [JsonPropertyName("features")] public List<FeatureMeta> Features { get; set; } = [];
}

public sealed class FeatureItem
{
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public string Desc { get; set; } = "";
    public string Rec { get; set; } = "keep";
    public string Group { get; set; } = "other";
    public string State { get; set; } = "unknown";
    public bool Known { get; set; }
}

public static class Features
{
    private static FeatureDb? _db;
    private static readonly object Gate = new();

    private static FeatureDb Db
    {
        get
        {
            lock (Gate)
            {
                if (_db is not null) return _db;
                var json = Res.Text("data/features.json");
                _db = string.IsNullOrWhiteSpace(json)
                    ? new FeatureDb()
                    : JsonSerializer.Deserialize<FeatureDb>(json, Store.Options) ?? new FeatureDb();
                return _db;
            }
        }
    }

    public static List<FeatureItem> List(string lang, bool refresh = false)
    {
        if (refresh) Cached.Drop("features.raw");
        var raw = Cached.Get("features.raw", TimeSpan.FromSeconds(60), ReadStates);

        var known = Db.Features.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
        var items = new List<FeatureItem>();

        foreach (var meta in Db.Features)
        {
            if (!raw.TryGetValue(meta.Name, out var state)) continue;
            items.Add(new FeatureItem
            {
                Name = meta.Name,
                Title = lang == "en" ? meta.En : meta.Ru,
                Desc = lang == "en" ? meta.DEn : meta.DRu,
                Rec = meta.Rec,
                Group = meta.Group,
                State = state,
                Known = true
            });
        }

        foreach (var (name, state) in raw)
        {
            if (known.ContainsKey(name)) continue;
            if (!state.Equals("enabled", StringComparison.OrdinalIgnoreCase)) continue;
            items.Add(new FeatureItem
            {
                Name = name,
                Title = name,
                Desc = "",
                Rec = "keep",
                Group = "other",
                State = state,
                Known = false
            });
        }

        return items;
    }

    private static Dictionary<string, string> ReadStates()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var r = Sh.Ps("Get-WindowsOptionalFeature -Online | ForEach-Object { \"{0}`t{1}\" -f $_.FeatureName, $_.State }", 180000);
        if (!r.Ok) return map;

        foreach (var line in r.Out.Split('\n'))
        {
            var parts = line.Split('\t');
            if (parts.Length < 2) continue;
            var name = parts[0].Trim();
            var state = parts[1].Trim();
            if (name.Length == 0) continue;
            map[name] = state.StartsWith("Enabled", StringComparison.OrdinalIgnoreCase)
                ? "enabled"
                : state.StartsWith("Disabled", StringComparison.OrdinalIgnoreCase)
                    ? "disabled"
                    : state.ToLowerInvariant();
        }
        return map;
    }

    public static RepairResult Set(string name, bool enable)
    {
        var result = new RepairResult();
        if (string.IsNullOrWhiteSpace(name) || name.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.'))
        {
            result.Ok = false;
            result.Message = Texts.Pick("недопустимое имя компонента", "invalid feature name");
            return result;
        }

        var meta = Db.Features.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        var title = meta is null ? name : Texts.Pick(meta.Ru, meta.En);

        var cmd = enable
            ? $"Enable-WindowsOptionalFeature -Online -FeatureName '{name}' -NoRestart -All -ErrorAction Stop"
            : $"Disable-WindowsOptionalFeature -Online -FeatureName '{name}' -NoRestart -ErrorAction Stop";

        var r = Sh.Ps(cmd, 900000);
        Cached.Drop("features.raw");

        if (!r.Ok)
        {
            result.Ok = false;
            result.Message = Trim(r.All);
            return result;
        }

        var entry = new JournalEntry { TweakId = "feature." + name, Title = title, Group = "feature" };
        entry.Items.Add(new JournalItem
        {
            Kind = "feature",
            Target = name,
            PrevValue = enable ? "disabled" : "enabled"
        });
        Journal.Add(entry);

        result.Changed = 1;
        result.Message = enable ? Texts.Pick("компонент включён", "feature enabled") : Texts.Pick("компонент отключён", "feature disabled");
        if (r.All.Contains("restart", StringComparison.OrdinalIgnoreCase)) result.Details.Add("restart");
        return result;
    }

    public static void Revert(string name, string prevState)
    {
        var enable = prevState.Equals("enabled", StringComparison.OrdinalIgnoreCase);
        var cmd = enable
            ? $"Enable-WindowsOptionalFeature -Online -FeatureName '{name}' -NoRestart -All -ErrorAction Stop"
            : $"Disable-WindowsOptionalFeature -Online -FeatureName '{name}' -NoRestart -ErrorAction Stop";
        Sh.Ps(cmd, 900000);
        Cached.Drop("features.raw");
    }

    private static string Trim(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 200 ? s[..200] : s;
    }
}
