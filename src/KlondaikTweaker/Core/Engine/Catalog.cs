using System.Text.Json;
using KlondaikTweaker.Core.Model;

namespace KlondaikTweaker.Core.Engine;

public static class Catalog
{
    private static TweakDb? _db;
    private static readonly object Gate = new();

    public static TweakDb Db
    {
        get
        {
            lock (Gate)
            {
                if (_db is not null) return _db;
                var json = Res.Text("data/tweaks.json");
                _db = string.IsNullOrWhiteSpace(json)
                    ? new TweakDb()
                    : JsonSerializer.Deserialize<TweakDb>(json, Store.Options) ?? new TweakDb();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _db.Tweaks.RemoveAll(t => t.Id.Length == 0 || t.Actions.Length == 0 || !seen.Add(t.Id));
                return _db;
            }
        }
    }

    public static TweakDef? Find(string id) => Db.Tweaks.FirstOrDefault(x => x.Id == id);

    public static IEnumerable<string> Categories => Db.Tweaks.Select(x => x.Cat).Distinct();
}
