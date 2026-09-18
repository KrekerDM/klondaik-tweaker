using System.Runtime.InteropServices;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class BoostState
{
    public bool Active { get; set; }
    public DateTime Started { get; set; }
    public string? PrevPlan { get; set; }
    public Dictionary<string, string> Services { get; set; } = [];
    public long FreedRam { get; set; }
}

public static class GameBoost
{
    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetTimerResolution(uint desired, bool set, out uint current);

    private static readonly string StatePath = Path.Combine(Paths.Root, "boost.json");

    private static readonly string[] Suspendable =
    [
        "SysMain", "WSearch", "Spooler", "PcaSvc", "DiagTrack", "WerSvc", "wuauserv", "DoSvc",
        "BITS", "MapsBroker", "TabletInputService", "dmwappushservice", "DusmSvc", "TrkWks",
        "WpcMonSvc", "RetailDemo", "Fax", "WMPNetworkSvc", "SharedAccess", "lfsvc", "PhoneSvc",
        "DPS", "WdiServiceHost", "WdiSystemHost", "diagnosticshub.standardcollector.service"
    ];

    public static BoostState State => Store.Load(StatePath, () => new BoostState());

    public static BoostState Start()
    {
        var st = new BoostState { Active = true, Started = DateTime.UtcNow };

        try
        {
            var cur = Sh.Run("powercfg.exe", "/getactivescheme", 10000).Out;
            var i = cur.IndexOf(':');
            var j = cur.IndexOf('(');
            if (i >= 0 && j > i) st.PrevPlan = cur[(i + 1)..j].Trim();
        }
        catch { }

        foreach (var s in Suspendable)
        {
            try
            {
                if (!Svc.Exists(s)) continue;
                var status = Svc.GetStatus(s);
                if (status != "running") continue;
                if (Svc.Stop(s, 6000)) st.Services[s] = "running";
            }
            catch { }
        }

        try
        {
            var ultimate = Sh.Run("powercfg.exe", "/list", 10000).Out;
            var guid = ExtractPlan(ultimate, "e9a42b02-d5df-448d-aa00-03f14749eb61")
                       ?? ExtractPlan(ultimate, "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
            if (guid is null)
            {
                Sh.Run("powercfg.exe", "-duplicatescheme e9a42b02-d5df-448d-aa00-03f14749eb61", 15000);
                ultimate = Sh.Run("powercfg.exe", "/list", 10000).Out;
                guid = ExtractPlan(ultimate, "e9a42b02-d5df-448d-aa00-03f14749eb61");
            }
            if (guid is not null) Sh.Run("powercfg.exe", "/setactive " + guid, 10000);
        }
        catch { }

        try { NtSetTimerResolution(5000, true, out _); } catch { }

        var trim = HwMonitor.TrimMemory();
        st.FreedRam = trim.Freed;

        Store.Save(StatePath, st);
        return st;
    }

    private static string? ExtractPlan(string list, string guid)
    {
        foreach (var line in list.Split('\n'))
        {
            if (line.Contains(guid, StringComparison.OrdinalIgnoreCase)) return guid;
        }
        return null;
    }

    public static BoostState Stop()
    {
        var st = State;
        foreach (var kv in st.Services)
        {
            try { Svc.Start(kv.Key, 8000); } catch { }
        }
        if (!string.IsNullOrWhiteSpace(st.PrevPlan))
        {
            try { Sh.Run("powercfg.exe", "/setactive " + st.PrevPlan, 10000); } catch { }
        }
        try { NtSetTimerResolution(5000, false, out _); } catch { }
        var cleared = new BoostState();
        Store.Save(StatePath, cleared);
        return cleared;
    }

    public static void RestoreIfStale()
    {
        var st = State;
        if (st.Active && (DateTime.UtcNow - st.Started).TotalHours > 8) Stop();
    }
}
