using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Engine;

public sealed class SysFacts
{
    public string OsName { get; set; } = "";
    public string OsVersion { get; set; } = "";
    public int Build { get; set; }
    public string Edition { get; set; } = "";
    public bool IsWin11 { get; set; }
    public string Cpu { get; set; } = "";
    public int Cores { get; set; }
    public int Threads { get; set; }
    public double RamGb { get; set; }
    public string[] Gpus { get; set; } = [];
    public bool Nvidia { get; set; }
    public bool Amd { get; set; }
    public bool Intel { get; set; }
    public bool Laptop { get; set; }
    public bool SystemSsd { get; set; }
    public string Motherboard { get; set; } = "";
    public string Host { get; set; } = "";
    public string User { get; set; } = "";
    public string PowerPlan { get; set; } = "";
    public bool DefenderReal { get; set; }
    public bool TamperProtection { get; set; }
    public bool VbsRunning { get; set; }
    public bool SecureBoot { get; set; }
    public string Arch { get; set; } = "";
}

public static class Env
{
    private static SysFacts? _cache;
    private static readonly object Gate = new();

    public static SysFacts Facts
    {
        get { lock (Gate) return _cache ??= Build(); }
    }

    public static void Invalidate() { lock (Gate) _cache = null; }

    private static SysFacts Build()
    {
        var f = new SysFacts
        {
            Host = Environment.MachineName,
            User = Environment.UserName,
            Arch = Environment.Is64BitOperatingSystem ? "x64" : "x86",
            Threads = Environment.ProcessorCount
        };

        const string cv = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion";
        f.Edition = Reg.Read("HKLM", cv, "ProductName").Value;
        int.TryParse(Reg.Read("HKLM", cv, "CurrentBuildNumber").Value, out var build);
        f.Build = build;
        var display = Reg.Read("HKLM", cv, "DisplayVersion").Value;
        var ubr = Reg.Read("HKLM", cv, "UBR").Value;
        f.IsWin11 = build >= 22000;
        if (f.IsWin11 && f.Edition.Contains("Windows 10")) f.Edition = f.Edition.Replace("Windows 10", "Windows 11");
        f.OsName = f.Edition;
        f.OsVersion = $"{(f.IsWin11 ? "11" : "10")} {display} (build {build}{(ubr.Length > 0 ? "." + ubr : "")})".Replace("  ", " ");

        f.Cpu = Reg.Read("HKLM", @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString").Value.Trim();
        f.Cores = Sys.PhysicalCores();
        if (f.Cores == 0) f.Cores = f.Threads;

        try { f.RamGb = Math.Round(Native.Memory().ullTotalPhys / 1024d / 1024d / 1024d, 1); } catch { }

        f.Gpus = Sys.Adapters();
        f.Nvidia = f.Gpus.Any(x => x.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase) || x.Contains("GeForce", StringComparison.OrdinalIgnoreCase));
        f.Amd = f.Gpus.Any(x => x.Contains("AMD", StringComparison.OrdinalIgnoreCase) || x.Contains("Radeon", StringComparison.OrdinalIgnoreCase));
        f.Intel = f.Gpus.Any(x => x.Contains("Intel", StringComparison.OrdinalIgnoreCase));

        const string bios = @"HARDWARE\DESCRIPTION\System\BIOS";
        var vendor = Reg.Read("HKLM", bios, "BaseBoardManufacturer").Value;
        var product = Reg.Read("HKLM", bios, "BaseBoardProduct").Value;
        f.Motherboard = (vendor + " " + product).Trim();

        f.Laptop = Sys.HasBattery();

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var letter = windows.Length > 0 ? windows[0] : 'C';
        f.SystemSsd = Sys.IsSolidState(letter) ?? Reg.Read("HKLM", @"SYSTEM\CurrentControlSet\Control\FileSystem", "NtfsDisableLastAccessUpdate").Exists;

        try
        {
            var r = Sh.Run("powercfg.exe", "/getactivescheme", 8000);
            var open = r.Out.IndexOf('(');
            var close = r.Out.LastIndexOf(')');
            f.PowerPlan = open >= 0 && close > open ? r.Out[(open + 1)..close] : r.Out.Trim();
        }
        catch { }

        const string defender = @"SOFTWARE\Microsoft\Windows Defender";
        var tamper = Reg.Read("HKLM", defender + @"\Features", "TamperProtection");
        f.TamperProtection = tamper.Exists && tamper.Value == "5";
        var rtpOff = Reg.Read("HKLM", defender + @"\Real-Time Protection", "DisableRealtimeMonitoring");
        var policyOff = Reg.Read("HKLM", @"SOFTWARE\Policies\Microsoft\Windows Defender", "DisableAntiSpyware");
        var disabled = Reg.Read("HKLM", defender, "DisableAntiSpyware");
        f.DefenderReal = !(rtpOff.Exists && rtpOff.Value == "1")
                         && !(policyOff.Exists && policyOff.Value == "1")
                         && !(disabled.Exists && disabled.Value == "1")
                         && Svc.Exists("WinDefend");

        const string guard = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
        var vbs = Reg.Read("HKLM", guard, "EnableVirtualizationBasedSecurity");
        var hvci = Reg.Read("HKLM", guard + @"\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled");
        f.VbsRunning = (vbs.Exists && vbs.Value == "1") || (hvci.Exists && hvci.Value == "1");

        var secureBoot = Reg.Read("HKLM", @"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled");
        f.SecureBoot = secureBoot.Exists && secureBoot.Value == "1";

        return f;
    }

    public static bool Meets(string? req)
    {
        if (string.IsNullOrWhiteSpace(req)) return true;
        var f = Facts;
        foreach (var raw in req.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var neg = raw.StartsWith('!');
            var key = neg ? raw[1..] : raw;
            var ok = key.ToLowerInvariant() switch
            {
                "win11" => f.IsWin11,
                "win10" => !f.IsWin11,
                "laptop" => f.Laptop,
                "desktop" => !f.Laptop,
                "ssd" => f.SystemSsd,
                "hdd" => !f.SystemSsd,
                "nvidia" => f.Nvidia,
                "amd" => f.Amd,
                "intel" => f.Intel,
                "b22h2" => f.Build >= 22621,
                "b24h2" => f.Build >= 26100,
                _ => true
            };
            if (neg) ok = !ok;
            if (!ok) return false;
        }
        return true;
    }
}
