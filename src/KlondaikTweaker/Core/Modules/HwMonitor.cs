using System.Diagnostics;
using System.Runtime.InteropServices;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class Metrics
{
    public double Cpu { get; set; }
    public double Ram { get; set; }
    public long RamUsed { get; set; }
    public long RamTotal { get; set; }
    public double Gpu { get; set; }
    public double GpuMem { get; set; }
    public double Disk { get; set; }
    public double NetDown { get; set; }
    public double NetUp { get; set; }
    public int Processes { get; set; }
    public int Threads { get; set; }
    public double CpuTemp { get; set; }
    public double GpuTemp { get; set; }
    public long Uptime { get; set; }
}

public static class HwMonitor
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    private static long _prevIdle, _prevKernel, _prevUser;
    private static PdhCounter? _pdh;
    private static readonly object Gate = new();
    private static DateTime _lastTemp = DateTime.MinValue;
    private static double _cpuTemp, _gpuTemp;
    private static bool _nvidia;
    private static bool _nvidiaChecked;
    private static bool _acpiFailed;
    private static int _tick;
    private static int _processes;

    private static PdhCounter Counters()
    {
        if (_pdh is not null) return _pdh;
        var p = new PdhCounter();
        p.Add("gpu", @"\GPU Engine(*engtype_3D)\Utilization Percentage");
        p.Add("gpumem", @"\GPU Process Memory(*)\Dedicated Usage");
        p.Add("disk", @"\PhysicalDisk(_Total)\% Disk Time");
        p.Add("netin", @"\Network Interface(*)\Bytes Received/sec");
        p.Add("netout", @"\Network Interface(*)\Bytes Sent/sec");
        p.Collect();
        _pdh = p;
        return p;
    }

    public static double CpuUsage()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
        var dIdle = idle - _prevIdle;
        var dKernel = kernel - _prevKernel;
        var dUser = user - _prevUser;
        _prevIdle = idle;
        _prevKernel = kernel;
        _prevUser = user;
        var total = dKernel + dUser;
        if (total <= 0) return 0;
        var used = total - dIdle;
        return Math.Clamp(used * 100.0 / total, 0, 100);
    }

    public static Metrics Read()
    {
        lock (Gate)
        {
            var m = new Metrics();
            m.Cpu = Math.Round(CpuUsage(), 1);

            var mem = Native.Memory();
            m.RamTotal = (long)mem.ullTotalPhys;
            m.RamUsed = (long)(mem.ullTotalPhys - mem.ullAvailPhys);
            m.Ram = mem.ullTotalPhys > 0 ? Math.Round(m.RamUsed * 100.0 / m.RamTotal, 1) : 0;

            try
            {
                var p = Counters();
                p.Collect();
                m.Gpu = Math.Round(Math.Min(100, p.Sum("gpu")), 1);
                m.GpuMem = Math.Round(p.Sum("gpumem") / 1024 / 1024, 0);
                m.Disk = Math.Round(Math.Min(100, p.Max("disk")), 1);
                m.NetDown = Math.Round(p.Sum("netin") / 1024, 1);
                m.NetUp = Math.Round(p.Sum("netout") / 1024, 1);
            }
            catch { }

            if (++_tick % 4 == 1)
            {
                try { _processes = Process.GetProcesses().Length; }
                catch { }
            }
            m.Processes = _processes;

            m.Uptime = (long)(Native.GetTickCount64() / 1000);

            if ((DateTime.UtcNow - _lastTemp).TotalSeconds > 25)
            {
                _lastTemp = DateTime.UtcNow;
                Task.Run(ReadTemps);
            }
            m.CpuTemp = _cpuTemp;
            m.GpuTemp = _gpuTemp;
            return m;
        }
    }

    private static void ReadTemps()
    {
        if (!_acpiFailed)
        {
            var r = Sh.Ps("(Get-CimInstance -Namespace root/wmi -ClassName MSAcpi_ThermalZoneTemperature -ErrorAction Stop | Measure-Object -Property CurrentTemperature -Maximum).Maximum", 20000);
            if (r.Ok && double.TryParse(r.Out.Trim(), out var raw) && raw > 0)
            {
                var celsius = (raw - 2732) / 10.0;
                if (celsius > 0 && celsius < 125) _cpuTemp = Math.Round(celsius, 1);
            }
            else
            {
                _acpiFailed = true;
            }
        }

        if (!_nvidiaChecked)
        {
            _nvidiaChecked = true;
            _nvidia = GpuTool.HasNvidiaSmi();
        }
        if (_nvidia)
        {
            var r = Sh.Run(GpuTool.NvidiaSmi, "--query-gpu=temperature.gpu --format=csv,noheader,nounits", 8000);
            if (r.Ok && double.TryParse(r.Out.Split('\n')[0].Trim(), out var t)) _gpuTemp = t;
        }
    }

    public static (int Trimmed, long Freed) TrimMemory()
    {
        Native.EnablePrivilege("SeProfileSingleProcessPrivilege");
        Native.EnablePrivilege("SeIncreaseQuotaPrivilege");
        var before = (long)Native.Memory().ullAvailPhys;
        int n = 0;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id <= 4) continue;
                Native.TrimProcess(p.Handle);
                n++;
            }
            catch { }
            finally { try { p.Dispose(); } catch { } }
        }
        Native.PurgeStandbyList();
        Native.FlushModifiedList();
        Thread.Sleep(400);
        var after = (long)Native.Memory().ullAvailPhys;
        return (n, Math.Max(0, after - before));
    }

    public static List<object> TopProcesses(int count = 12)
    {
        var list = new List<object>();
        try
        {
            foreach (var p in Process.GetProcesses().OrderByDescending(x =>
            {
                try { return x.WorkingSet64; } catch { return 0L; }
            }).Take(count))
            {
                try
                {
                    list.Add(new { name = p.ProcessName, pid = p.Id, ram = p.WorkingSet64, threads = p.Threads.Count });
                }
                catch { }
                finally { try { p.Dispose(); } catch { } }
            }
        }
        catch { }
        return list;
    }
}

public static class GpuTool
{
    public static string NvidiaSmi => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");

    public static bool HasNvidiaSmi()
    {
        try { return File.Exists(NvidiaSmi); } catch { return false; }
    }
}
