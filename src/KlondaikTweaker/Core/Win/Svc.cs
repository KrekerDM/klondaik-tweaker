using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;

namespace KlondaikTweaker.Core.Win;

public sealed class SvcInfo
{
    public string Name { get; set; } = "";
    public string Display { get; set; } = "";
    public string Desc { get; set; } = "";
    public string Start { get; set; } = "";
    public string Status { get; set; } = "";
    public bool Protected { get; set; }
}

public static class Svc
{
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int SHLoadIndirectString(string pszSource, StringBuilder pszOutBuf, int cchOutBuf, IntPtr ppvReserved);

    private const string Base = @"SYSTEM\CurrentControlSet\Services";

    public static string Resolve(string s)
    {
        if (string.IsNullOrEmpty(s) || s[0] != '@') return s;
        var sb = new StringBuilder(1024);
        return SHLoadIndirectString(s, sb, sb.Capacity, IntPtr.Zero) == 0 ? sb.ToString() : "";
    }

    public static string ModeName(int start, bool delayed) => start switch
    {
        0 => "boot",
        1 => "system",
        2 => delayed ? "delayed" : "auto",
        3 => "manual",
        4 => "disabled",
        _ => "unknown"
    };

    public static int ModeValue(string mode) => mode.ToLowerInvariant() switch
    {
        "boot" => 0,
        "system" => 1,
        "auto" => 2,
        "delayed" => 2,
        "manual" => 3,
        "disabled" => 4,
        _ => 3
    };

    public static string GetStart(string name)
    {
        var s = Reg.Read("HKLM", $@"{Base}\{name}", "Start");
        if (!s.Exists) return "missing";
        var d = Reg.Read("HKLM", $@"{Base}\{name}", "DelayedAutostart");
        var delayed = d.Exists && d.Value == "1";
        return ModeName((int)Reg.ParseNum(s.Value), delayed);
    }

    public static string GetStatus(string name)
    {
        try
        {
            using var sc = new ServiceController(name);
            return sc.Status.ToString().ToLowerInvariant();
        }
        catch { return "unknown"; }
    }

    public static bool Exists(string name) => Reg.KeyExists("HKLM", $@"{Base}\{name}");

    public static void SetStart(string name, string mode)
    {
        if (!Exists(name)) throw new InvalidOperationException("service not found: " + name);
        Reg.Write("HKLM", $@"{Base}\{name}", "Start", "dword", ModeValue(mode).ToString());
        if (mode.Equals("delayed", StringComparison.OrdinalIgnoreCase))
            Reg.Write("HKLM", $@"{Base}\{name}", "DelayedAutostart", "dword", "1");
        else if (mode.Equals("auto", StringComparison.OrdinalIgnoreCase))
            Reg.Write("HKLM", $@"{Base}\{name}", "DelayedAutostart", "dword", "0");
    }

    public static bool Stop(string name, int waitMs = 12000)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status == ServiceControllerStatus.Stopped) return true;
            if (!sc.CanStop) return false;
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMilliseconds(waitMs));
            return true;
        }
        catch { return false; }
    }

    public static bool Start(string name, int waitMs = 15000)
    {
        try
        {
            using var sc = new ServiceController(name);
            if (sc.Status == ServiceControllerStatus.Running) return true;
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMilliseconds(waitMs));
            return true;
        }
        catch { return false; }
    }

    public static List<SvcInfo> All()
    {
        var list = new List<SvcInfo>();
        foreach (var name in Reg.SubKeys("HKLM", Base))
        {
            var path = $@"{Base}\{name}";
            var type = Reg.Read("HKLM", path, "Type");
            if (!type.Exists) continue;
            var t = (int)Reg.ParseNum(type.Value);
            if ((t & 0x3) == 0 && (t & 0x10) == 0 && (t & 0x20) == 0) continue;
            var start = Reg.Read("HKLM", path, "Start");
            if (!start.Exists) continue;
            var disp = Reg.Read("HKLM", path, "DisplayName").Value;
            var desc = Reg.Read("HKLM", path, "Description").Value;
            var delayed = Reg.Read("HKLM", path, "DelayedAutostart");
            list.Add(new SvcInfo
            {
                Name = name,
                Display = Resolve(disp) is { Length: > 0 } r ? r : (disp.Length > 0 ? disp : name),
                Desc = Resolve(desc),
                Start = ModeName((int)Reg.ParseNum(start.Value), delayed.Exists && delayed.Value == "1"),
                Status = ""
            });
        }
        try
        {
            var running = ServiceController.GetServices().ToDictionary(x => x.ServiceName, x => x.Status.ToString().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase);
            foreach (var s in list) if (running.TryGetValue(s.Name, out var st)) s.Status = st;
        }
        catch { }
        return list.OrderBy(x => x.Display, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
