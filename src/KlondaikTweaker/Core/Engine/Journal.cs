using KlondaikTweaker.Core.Model;

namespace KlondaikTweaker.Core.Engine;

public sealed class JournalFile
{
    public int Version { get; set; } = 1;
    public List<JournalEntry> Entries { get; set; } = [];
}

public static class Journal
{
    private static JournalFile _file = Store.Load(Paths.Journal, () => new JournalFile());
    private static readonly object Gate = new();

    public static IReadOnlyList<JournalEntry> Entries
    {
        get { lock (Gate) return _file.Entries.ToList(); }
    }

    public static void Add(JournalEntry e)
    {
        lock (Gate)
        {
            var existing = _file.Entries.LastOrDefault(x => x.TweakId == e.TweakId && !x.Reverted && x.Group == e.Group);
            if (existing is not null)
            {
                foreach (var item in e.Items)
                {
                    var known = existing.Items.Any(x =>
                        x.Kind == item.Kind &&
                        string.Equals(x.Target, item.Target, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.Name ?? "", item.Name ?? "", StringComparison.OrdinalIgnoreCase));
                    if (!known) existing.Items.Add(item);
                }
                existing.Utc = DateTime.UtcNow;
            }
            else
            {
                _file.Entries.Add(e);
            }
            if (_file.Entries.Count > 4000) _file.Entries.RemoveRange(0, _file.Entries.Count - 4000);
            Store.Save(Paths.Journal, _file);
        }
    }

    public static JournalEntry? Latest(string tweakId)
    {
        lock (Gate) return _file.Entries.Where(x => x.TweakId == tweakId && !x.Reverted).OrderByDescending(x => x.Utc).FirstOrDefault();
    }

    public static JournalEntry? ById(string id)
    {
        lock (Gate) return _file.Entries.FirstOrDefault(x => x.Id == id);
    }

    public static void MarkReverted(string id)
    {
        lock (Gate)
        {
            var e = _file.Entries.FirstOrDefault(x => x.Id == id);
            if (e is null) return;
            e.Reverted = true;
            Store.Save(Paths.Journal, _file);
        }
    }

    public static bool IsApplied(string tweakId) => Latest(tweakId) is not null;

    public static void Clear()
    {
        lock (Gate)
        {
            _file.Entries.Clear();
            Store.Save(Paths.Journal, _file);
        }
    }
}
