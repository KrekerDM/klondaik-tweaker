using System.Text.Json;

namespace KlondaikTweaker.Core.Engine;

public static class Store
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly object Gate = new();

    public static T Load<T>(string path, Func<T> fallback)
    {
        try
        {
            lock (Gate)
            {
                if (!File.Exists(path)) return fallback();
                var json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json)) return fallback();
                return JsonSerializer.Deserialize<T>(json, Options) ?? fallback();
            }
        }
        catch { return fallback(); }
    }

    public static void Save<T>(string path, T value)
    {
        try
        {
            lock (Gate)
            {
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(value, Options));
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
            }
        }
        catch { }
    }
}
