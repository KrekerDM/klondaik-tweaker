namespace KlondaikTweaker.Core.Engine;

public static class Cached
{
    private sealed record Entry(object Value, DateTime Until);

    private static readonly Dictionary<string, Entry> Map = new(StringComparer.Ordinal);
    private static readonly object Gate = new();

    public static T Get<T>(string key, TimeSpan ttl, Func<T> factory) where T : notnull
    {
        lock (Gate)
        {
            if (Map.TryGetValue(key, out var hit) && hit.Until > DateTime.UtcNow && hit.Value is T typed)
                return typed;
        }

        var value = factory();

        lock (Gate)
        {
            Map[key] = new Entry(value, DateTime.UtcNow.Add(ttl));
        }
        return value;
    }

    public static void Drop(string key)
    {
        lock (Gate) Map.Remove(key);
    }

    public static void DropAll()
    {
        lock (Gate) Map.Clear();
    }
}
