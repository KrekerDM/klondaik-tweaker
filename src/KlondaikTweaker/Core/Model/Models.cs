using System.Text.Json.Serialization;

namespace KlondaikTweaker.Core.Model;

public enum TweakState { Unknown, Applied, NotApplied, Partial, Unavailable }

public sealed class Loc
{
    [JsonPropertyName("t")] public string T { get; set; } = "";
    [JsonPropertyName("d")] public string D { get; set; } = "";
    [JsonPropertyName("w")] public string? W { get; set; }
}

public sealed class TweakAction
{
    [JsonPropertyName("k")] public string K { get; set; } = "reg";
    [JsonPropertyName("h")] public string? H { get; set; }
    [JsonPropertyName("p")] public string? P { get; set; }
    [JsonPropertyName("n")] public string? N { get; set; }
    [JsonPropertyName("t")] public string? T { get; set; }
    [JsonPropertyName("v")] public string? V { get; set; }
    [JsonPropertyName("d")] public string? D { get; set; }
    [JsonPropertyName("exe")] public string? Exe { get; set; }
    [JsonPropertyName("args")] public string? Args { get; set; }
    [JsonPropertyName("rargs")] public string? RArgs { get; set; }
    [JsonPropertyName("stop")] public bool Stop { get; set; }
    [JsonPropertyName("dk")] public bool Dk { get; set; }
}

public sealed class TweakDef
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("cat")] public string Cat { get; set; } = "";
    [JsonPropertyName("risk")] public string Risk { get; set; } = "safe";
    [JsonPropertyName("restart")] public bool Restart { get; set; }
    [JsonPropertyName("logoff")] public bool Logoff { get; set; }
    [JsonPropertyName("tags")] public string[] Tags { get; set; } = [];
    [JsonPropertyName("src")] public string? Src { get; set; }
    [JsonPropertyName("req")] public string? Req { get; set; }
    [JsonPropertyName("ru")] public Loc Ru { get; set; } = new();
    [JsonPropertyName("en")] public Loc En { get; set; } = new();
    [JsonPropertyName("actions")] public TweakAction[] Actions { get; set; } = [];
}

public sealed class TweakDb
{
    [JsonPropertyName("version")] public int Version { get; set; }
    [JsonPropertyName("tweaks")] public List<TweakDef> Tweaks { get; set; } = [];
}

public sealed class TweakView
{
    public string Id { get; set; } = "";
    public string Cat { get; set; } = "";
    public string Risk { get; set; } = "safe";
    public string Title { get; set; } = "";
    public string Desc { get; set; } = "";
    public string? Warn { get; set; }
    public string[] Tags { get; set; } = [];
    public string? Src { get; set; }
    public bool Restart { get; set; }
    public bool Logoff { get; set; }
    public string State { get; set; } = "unknown";
    public bool Available { get; set; } = true;
    public string? Note { get; set; }
}

public sealed class JournalItem
{
    public string Kind { get; set; } = "";
    public string Target { get; set; } = "";
    public string? Name { get; set; }
    public bool Existed { get; set; }
    public string? PrevType { get; set; }
    public string? PrevValue { get; set; }
    public string? Revert { get; set; }
}

public sealed class JournalEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");
    public string TweakId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Group { get; set; } = "tweak";
    public DateTime Utc { get; set; } = DateTime.UtcNow;
    public bool Reverted { get; set; }
    public List<JournalItem> Items { get; set; } = [];
}

public sealed class ActionResult
{
    public bool Ok { get; set; } = true;
    public string? Error { get; set; }
    public bool NeedsRestart { get; set; }
    public string? Note { get; set; }
}
