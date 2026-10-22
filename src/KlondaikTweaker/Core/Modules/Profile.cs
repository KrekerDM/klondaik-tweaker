using System.Text.Json.Serialization;
using KlondaikTweaker.Core.Engine;

namespace KlondaikTweaker.Core.Modules;

public sealed class ProfileFile
{
    [JsonPropertyName("kind")] public string Kind { get; set; } = Profile.Kind;
    [JsonPropertyName("version")] public int Version { get; set; } = 1;
    [JsonPropertyName("app")] public string App { get; set; } = "";
    [JsonPropertyName("windows")] public string Windows { get; set; } = "";
    [JsonPropertyName("created")] public string Created { get; set; } = "";
    [JsonPropertyName("tweaks")] public List<string> Tweaks { get; set; } = [];
}

public static class Profile
{
    public const string Kind = "klondaik-profile";

    public static string Folder
    {
        get
        {
            var dir = Path.Combine(Paths.Root, "profiles");
            try { Directory.CreateDirectory(dir); } catch { }
            return dir;
        }
    }

    public static string SuggestedName() => "klondaik-" + DateTime.Now.ToString("yyyy-MM-dd") + ".json";

    public static List<string> Applied() =>
        Journal.Entries
            .Where(x => !x.Reverted)
            .GroupBy(x => x.TweakId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.Key)
            .Where(id => Catalog.Find(id) is not null)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

    public static ProfileFile Build(IEnumerable<string> ids) => new()
    {
        App = typeof(Profile).Assembly.GetName().Version?.ToString(3) ?? "1.0.0",
        Windows = Env.Facts.OsVersion,
        Created = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
        Tweaks = ids.ToList()
    };

    public static void Write(string path, ProfileFile file)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(file, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
    }

    public static (ProfileFile? File, string? Error) Read(string path)
    {
        if (!File.Exists(path))
            return (null, Texts.Pick("файл не найден", "file not found"));

        var info = new FileInfo(path);
        if (info.Length > 256 * 1024)
            return (null, Texts.Pick("файл слишком большой для набора твиков", "too large for a tweak set"));

        ProfileFile? parsed;
        try
        {
            parsed = System.Text.Json.JsonSerializer.Deserialize<ProfileFile>(File.ReadAllText(path), Store.Options);
        }
        catch
        {
            return (null, Texts.Pick("файл не читается как набор твиков", "the file does not read as a tweak set"));
        }

        if (parsed is null || !string.Equals(parsed.Kind, Kind, StringComparison.OrdinalIgnoreCase))
            return (null, Texts.Pick("это не набор твиков Klondaik", "not a Klondaik tweak set"));
        if (parsed.Version > 1)
            return (null, Texts.Pick("набор записан более новой версией программы", "the set was written by a newer version"));

        parsed.Tweaks = parsed.Tweaks
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(2000)
            .ToList();

        return (parsed, null);
    }
}
