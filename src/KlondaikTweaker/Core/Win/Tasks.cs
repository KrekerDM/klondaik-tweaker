using Microsoft.Win32.TaskScheduler;

namespace KlondaikTweaker.Core.Win;

public sealed class TaskInfo
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public string Author { get; set; } = "";
    public bool Enabled { get; set; }
    public string Trigger { get; set; } = "";
    public string Action { get; set; } = "";
}

public static class Tasks
{
    private static TaskService? _service;
    private static readonly object Gate = new();

    private static TaskService? Service
    {
        get
        {
            lock (Gate)
            {
                try { return _service ??= new TaskService(); }
                catch { return null; }
            }
        }
    }

    public static bool? GetEnabled(string path)
    {
        lock (Gate)
        {
            try
            {
                var t = Service?.GetTask(path);
                return t?.Enabled;
            }
            catch { return null; }
        }
    }

    public static bool SetEnabled(string path, bool enabled)
    {
        lock (Gate)
        {
            try
            {
                var t = Service?.GetTask(path);
                if (t is null) return false;
                t.Enabled = enabled;
                return true;
            }
            catch
            {
                return SetEnabledElevated(path, enabled);
            }
        }
    }

    private static bool SetEnabledElevated(string path, bool enabled)
    {
        try
        {
            var flag = enabled ? "/enable" : "/disable";
            var r = Ti.Run($"schtasks /change /tn \"{path}\" {flag}", 45000);
            return r.Ok;
        }
        catch { return false; }
    }

    public static List<TaskInfo> Startup()
    {
        var list = new List<TaskInfo>();
        lock (Gate)
        {
            try
            {
                var root = Service?.RootFolder;
                if (root is not null) Walk(root, list);
            }
            catch { }
        }
        return list;
    }

    private static void Walk(TaskFolder folder, List<TaskInfo> list)
    {
        foreach (var t in folder.Tasks)
        {
            try
            {
                var triggers = t.Definition.Triggers;
                bool boot = triggers.Any(x => x.TriggerType is TaskTriggerType.Logon or TaskTriggerType.Boot or TaskTriggerType.SessionStateChange);
                if (!boot) continue;
                var act = t.Definition.Actions.FirstOrDefault()?.ToString() ?? "";
                list.Add(new TaskInfo
                {
                    Path = t.Path,
                    Name = t.Name,
                    Author = t.Definition.RegistrationInfo.Author ?? "",
                    Enabled = t.Enabled,
                    Trigger = string.Join(", ", triggers.Select(x => x.TriggerType.ToString())),
                    Action = act.Length > 160 ? act[..160] : act
                });
            }
            catch { }
        }
        foreach (var f in folder.SubFolders)
        {
            if (f.Name.Equals("Microsoft", StringComparison.OrdinalIgnoreCase)) continue;
            Walk(f, list);
        }
    }
}
