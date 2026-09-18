using System.Text.Json;
using System.Text.Json.Serialization;
using KlondaikTweaker.Core.Engine;

namespace KlondaikTweaker.Core.Modules;

public sealed class WizOption
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("ru")] public string Ru { get; set; } = "";
    [JsonPropertyName("en")] public string En { get; set; } = "";
    [JsonPropertyName("hru")] public string? HintRu { get; set; }
    [JsonPropertyName("hen")] public string? HintEn { get; set; }
}

public sealed class WizQuestion
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("multi")] public bool Multi { get; set; }
    [JsonPropertyName("ru")] public string Ru { get; set; } = "";
    [JsonPropertyName("en")] public string En { get; set; } = "";
    [JsonPropertyName("sru")] public string? SubRu { get; set; }
    [JsonPropertyName("sen")] public string? SubEn { get; set; }
    [JsonPropertyName("when")] public string? When { get; set; }
    [JsonPropertyName("auto")] public string? Auto { get; set; }
    [JsonPropertyName("options")] public List<WizOption> Options { get; set; } = [];
}

public sealed class WizRule
{
    [JsonPropertyName("when")] public string When { get; set; } = "";
    [JsonPropertyName("add")] public string[] Add { get; set; } = [];
    [JsonPropertyName("drop")] public string[] Drop { get; set; } = [];
}

public sealed class WizDb
{
    [JsonPropertyName("questions")] public List<WizQuestion> Questions { get; set; } = [];
    [JsonPropertyName("rules")] public List<WizRule> Rules { get; set; } = [];
    [JsonPropertyName("base")] public string[] Base { get; set; } = [];
}

public static class Wizard
{
    private static WizDb? _db;
    private static readonly object Gate = new();

    public static WizDb Db
    {
        get
        {
            lock (Gate)
            {
                if (_db is not null) return _db;
                var json = Res.Text("data/wizard.json");
                _db = string.IsNullOrWhiteSpace(json) ? new WizDb() : JsonSerializer.Deserialize<WizDb>(json, Store.Options) ?? new WizDb();
                return _db;
            }
        }
    }

    public static List<WizQuestion> Questions()
    {
        var facts = Env.Facts;
        var list = new List<WizQuestion>();
        foreach (var q in Db.Questions)
        {
            if (q.Auto is not null)
            {
                var auto = q.Auto.ToLowerInvariant() switch
                {
                    "laptop" => facts.Laptop,
                    "ssd" => facts.SystemSsd,
                    "win11" => facts.IsWin11,
                    "nvidia" => facts.Nvidia,
                    _ => false
                };
                if (auto) continue;
            }
            list.Add(q);
        }
        return list;
    }

    public static bool Eval(string? condition, Dictionary<string, List<string>> answers)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;
        foreach (var clause in condition.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = clause.Split(["!=", "~", "="], StringSplitOptions.None);
            if (parts.Length < 2) continue;
            var key = parts[0].Trim();
            var val = parts[^1].Trim();
            answers.TryGetValue(key, out var picked);
            picked ??= [];
            bool ok;
            if (clause.Contains("!=")) ok = !picked.Contains(val, StringComparer.OrdinalIgnoreCase);
            else ok = picked.Contains(val, StringComparer.OrdinalIgnoreCase);
            if (!ok) return false;
        }
        return true;
    }

    public static List<string> Resolve(Dictionary<string, List<string>> answers)
    {
        var facts = Env.Facts;
        var set = new HashSet<string>(Db.Base, StringComparer.OrdinalIgnoreCase);

        var implied = new Dictionary<string, List<string>>(answers, StringComparer.OrdinalIgnoreCase);
        void Imply(string key, string value)
        {
            if (implied.ContainsKey(key)) return;
            implied[key] = [value];
        }
        Imply("device", facts.Laptop ? "laptop" : "desktop");
        Imply("disk", facts.SystemSsd ? "ssd" : "hdd");
        Imply("gpu", facts.Nvidia ? "nvidia" : facts.Amd ? "amd" : "intel");

        foreach (var rule in Db.Rules)
        {
            if (!Eval(rule.When, implied)) continue;
            foreach (var a in rule.Add) set.Add(a);
            foreach (var d in rule.Drop) set.Remove(d);
        }

        var known = Catalog.Db.Tweaks.Select(x => x.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return set.Where(known.Contains)
                  .Where(id => Env.Meets(Catalog.Find(id)?.Req))
                  .OrderBy(x => x)
                  .ToList();
    }
}
