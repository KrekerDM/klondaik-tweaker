using System.Text.Json;
using System.Text.Json.Serialization;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Model;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class TaskGroupMeta
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("ru")] public string Ru { get; set; } = "";
    [JsonPropertyName("en")] public string En { get; set; } = "";
    [JsonPropertyName("dru")] public string DRu { get; set; } = "";
    [JsonPropertyName("den")] public string DEn { get; set; } = "";
    [JsonPropertyName("rec")] public string Rec { get; set; } = "keep";
    [JsonPropertyName("paths")] public List<string> Paths { get; set; } = [];
}

public sealed class TaskGroupDb
{
    [JsonPropertyName("groups")] public List<TaskGroupMeta> Groups { get; set; } = [];
}

public sealed class TaskRow
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
}

public sealed class TaskGroupView
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Desc { get; set; } = "";
    public string Rec { get; set; } = "keep";
    public int Enabled { get; set; }
    public int Missing { get; set; }
    public List<TaskRow> Tasks { get; set; } = [];
}

public static class TaskGroups
{
    private static TaskGroupDb? _db;
    private static readonly object Gate = new();

    private static TaskGroupDb Db
    {
        get
        {
            lock (Gate)
            {
                if (_db is not null) return _db;
                var json = Res.Text("data/tasks.json");
                _db = string.IsNullOrWhiteSpace(json)
                    ? new TaskGroupDb()
                    : JsonSerializer.Deserialize<TaskGroupDb>(json, Store.Options) ?? new TaskGroupDb();
                return _db;
            }
        }
    }

    public static List<TaskGroupView> List(string lang)
    {
        var result = new List<TaskGroupView>();

        foreach (var meta in Db.Groups)
        {
            var view = new TaskGroupView
            {
                Id = meta.Id,
                Title = lang == "en" ? meta.En : meta.Ru,
                Desc = lang == "en" ? meta.DEn : meta.DRu,
                Rec = meta.Rec
            };

            foreach (var path in meta.Paths)
            {
                var state = Tasks.GetEnabled(path);
                if (state is null)
                {
                    view.Missing++;
                    continue;
                }
                view.Tasks.Add(new TaskRow
                {
                    Path = path,
                    Name = path[(path.LastIndexOf('\\') + 1)..],
                    Enabled = state.Value
                });
                if (state.Value) view.Enabled++;
            }

            if (view.Tasks.Count > 0) result.Add(view);
        }

        return result;
    }

    public static RepairResult SetGroup(string id, bool enable, string lang)
    {
        var meta = Db.Groups.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (meta is null) return new RepairResult { Ok = false, Message = "группа не найдена: " + id };

        var title = lang == "en" ? meta.En : meta.Ru;
        var entry = new JournalEntry { TweakId = "tasks." + meta.Id, Title = title, Group = "tasks" };
        var result = new RepairResult();

        foreach (var path in meta.Paths)
        {
            var state = Tasks.GetEnabled(path);
            if (state is null) { result.Skipped++; continue; }
            if (state.Value == enable) { result.Skipped++; continue; }

            if (Tasks.SetEnabled(path, enable))
            {
                entry.Items.Add(new JournalItem { Kind = "task", Target = path, PrevValue = state.Value ? "on" : "off" });
                result.Changed++;
            }
            else
            {
                result.Details.Add(path);
            }
        }

        if (entry.Items.Count > 0) Journal.Add(entry);

        result.Message = result.Changed == 0
            ? (result.Details.Count > 0 ? "не удалось переключить задачи" : "уже в нужном состоянии")
            : (enable ? $"включено задач: {result.Changed}" : $"отключено задач: {result.Changed}");
        if (result.Changed == 0 && result.Details.Count > 0) result.Ok = false;
        return result;
    }

    public static RepairResult SetOne(string path, bool enable)
    {
        var known = Db.Groups.SelectMany(g => g.Paths)
            .Any(p => p.Equals(path, StringComparison.OrdinalIgnoreCase));
        if (!known) return new RepairResult { Ok = false, Message = "задача не входит в известные группы" };

        var state = Tasks.GetEnabled(path);
        if (state is null) return new RepairResult { Ok = false, Message = "задача не найдена в планировщике" };

        if (!Tasks.SetEnabled(path, enable))
            return new RepairResult { Ok = false, Message = "не удалось переключить задачу" };

        var entry = new JournalEntry
        {
            TweakId = "task." + path,
            Title = path[(path.LastIndexOf('\\') + 1)..],
            Group = "tasks"
        };
        entry.Items.Add(new JournalItem { Kind = "task", Target = path, PrevValue = state.Value ? "on" : "off" });
        Journal.Add(entry);

        return new RepairResult { Changed = 1, Message = enable ? "задача включена" : "задача отключена" };
    }
}
