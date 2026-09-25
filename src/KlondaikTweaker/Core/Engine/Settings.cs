namespace KlondaikTweaker.Core.Engine;

public sealed class SettingsData
{
    public string Lang { get; set; } = "ru";
    public bool ShowExtreme { get; set; } = true;
    public bool AutoRestorePoint { get; set; } = true;
    public bool Monitor3d { get; set; } = true;
    public bool LiveMonitor { get; set; } = true;
    public bool Reduced { get; set; }
    public bool WizardDone { get; set; }
    public bool AcceptedRisk { get; set; }
    public string Accent { get; set; } = "ice";
    public List<string> Favorites { get; set; } = [];
    public string? LastProfile { get; set; }
    public bool AutoUpdateCheck { get; set; } = true;
    public string? LastUpdateCheck { get; set; }
}

public static class Settings
{
    private static SettingsData _data = Store.Load(Paths.Settings, () => new SettingsData());
    private static readonly object Gate = new();

    public static SettingsData Data { get { lock (Gate) return _data; } }

    public static void Update(Action<SettingsData> mutate)
    {
        lock (Gate)
        {
            mutate(_data);
            Store.Save(Paths.Settings, _data);
        }
    }
}
