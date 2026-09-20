using KlondaikTweaker.Core.Engine;
using System.Net.NetworkInformation;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class DnsPreset
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Primary { get; set; } = "";
    public string Secondary { get; set; } = "";
    public string NoteRu { get; set; } = "";
    public string NoteEn { get; set; } = "";
    public long Ping { get; set; } = -1;
}

public static class NetworkTools
{
    private const string IfBase = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";

    public static readonly string[] TelemetryHosts =
    [
        "vortex.data.microsoft.com", "vortex-win.data.microsoft.com", "telecommand.telemetry.microsoft.com",
        "oca.telemetry.microsoft.com", "sqm.telemetry.microsoft.com", "watson.telemetry.microsoft.com",
        "settings-sandbox.data.microsoft.com", "telemetry.microsoft.com", "telemetry.urs.microsoft.com",
        "df.telemetry.microsoft.com", "reports.wes.df.telemetry.microsoft.com", "services.wes.df.telemetry.microsoft.com",
        "sqm.df.telemetry.microsoft.com", "watson.ppe.telemetry.microsoft.com", "telemetry.appex.bing.net",
        "telemetry.appex.bing.net:443", "choice.microsoft.com", "choice.microsoft.com.nsatc.net",
        "vortex-sandbox.data.microsoft.com", "settings-win.data.microsoft.com", "survey.watson.microsoft.com",
        "watson.live.com", "watson.microsoft.com", "statsfe2.ws.microsoft.com", "statsfe1.ws.microsoft.com",
        "corp.sts.microsoft.com", "compatexchange.cloudapp.net", "diagnostics.support.microsoft.com",
        "feedback.windows.com", "feedback.microsoft-hohm.com", "feedback.search.microsoft.com"
    ];

    public static List<DnsPreset> DnsPresets() =>
    [
        new() { Id = "cloudflare", Name = "Cloudflare", Primary = "1.1.1.1", Secondary = "1.0.0.1", NoteRu = "Быстрый и без логов", NoteEn = "Fast, no logs" },
        new() { Id = "google", Name = "Google", Primary = "8.8.8.8", Secondary = "8.8.4.4", NoteRu = "Стабильный, есть везде", NoteEn = "Stable everywhere" },
        new() { Id = "quad9", Name = "Quad9", Primary = "9.9.9.9", Secondary = "149.112.112.112", NoteRu = "Блокирует вредоносные домены", NoteEn = "Blocks malicious domains" },
        new() { Id = "adguard", Name = "AdGuard", Primary = "94.140.14.14", Secondary = "94.140.15.15", NoteRu = "Режет рекламу на уровне DNS", NoteEn = "Blocks ads at DNS level" },
        new() { Id = "yandex", Name = "Yandex", Primary = "77.88.8.8", Secondary = "77.88.8.1", NoteRu = "Ближе для РФ и СНГ", NoteEn = "Closer for RU and CIS" },
        new() { Id = "comss", Name = "Comss.one", Primary = "83.220.169.155", Secondary = "212.109.195.93", NoteRu = "Обход блокировок, DNSCrypt", NoteEn = "Unblocking DNS" }
    ];

    public static List<object> Adapters()
    {
        var res = new List<object>();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()
                         .OrderByDescending(x => x.OperationalStatus == OperationalStatus.Up))
            {
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                var props = ni.GetIPProperties();
                var dns = props.DnsAddresses.Where(x => x.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(x => x.ToString()).ToArray();
                var ip = props.UnicastAddresses.Where(x => x.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).Select(x => x.Address.ToString()).FirstOrDefault() ?? "";
                if (ni.OperationalStatus != OperationalStatus.Up && ip.Length == 0) continue;
                res.Add(new
                {
                    id = ni.Id,
                    name = ni.Name,
                    desc = ni.Description,
                    up = ni.OperationalStatus == OperationalStatus.Up,
                    speed = ni.Speed > 0 ? ni.Speed / 1_000_000 : 0,
                    ip,
                    dns,
                    nagle = NagleState(ni.Id)
                });
            }
        }
        catch { }
        return res;
    }

    private static bool NagleState(string guid)
    {
        var a = Reg.Read("HKLM", $@"{IfBase}\{guid}", "TcpAckFrequency");
        var b = Reg.Read("HKLM", $@"{IfBase}\{guid}", "TCPNoDelay");
        return a.Exists && a.Value == "1" && b.Exists && b.Value == "1";
    }

    public static void SetNagle(bool disabled)
    {
        foreach (var guid in Reg.SubKeys("HKLM", IfBase))
        {
            try
            {
                if (disabled)
                {
                    Reg.Write("HKLM", $@"{IfBase}\{guid}", "TcpAckFrequency", "dword", "1");
                    Reg.Write("HKLM", $@"{IfBase}\{guid}", "TCPNoDelay", "dword", "1");
                    Reg.Write("HKLM", $@"{IfBase}\{guid}", "TcpDelAckTicks", "dword", "0");
                }
                else
                {
                    Reg.DeleteValue("HKLM", $@"{IfBase}\{guid}", "TcpAckFrequency");
                    Reg.DeleteValue("HKLM", $@"{IfBase}\{guid}", "TCPNoDelay");
                    Reg.DeleteValue("HKLM", $@"{IfBase}\{guid}", "TcpDelAckTicks");
                }
            }
            catch { }
        }
    }

    public static string SetDns(string adapterName, string primary, string secondary)
    {
        if (string.IsNullOrWhiteSpace(primary))
        {
            var r0 = Sh.Run("netsh.exe", $"interface ipv4 set dnsservers name=\"{adapterName}\" source=dhcp", 30000);
            return r0.Ok ? "ok" : r0.All;
        }
        var r = Sh.Run("netsh.exe", $"interface ipv4 set dnsservers name=\"{adapterName}\" static {primary} primary validate=no", 30000);
        if (!r.Ok) return r.All;
        if (!string.IsNullOrWhiteSpace(secondary))
            Sh.Run("netsh.exe", $"interface ipv4 add dnsservers name=\"{adapterName}\" {secondary} index=2 validate=no", 30000);
        Sh.Run("ipconfig.exe", "/flushdns", 15000);
        return "ok";
    }

    public static List<DnsPreset> TestDns()
    {
        var list = DnsPresets();
        Parallel.ForEach(list, p =>
        {
            try
            {
                using var ping = new Ping();
                long best = -1;
                for (int i = 0; i < 3; i++)
                {
                    var r = ping.Send(p.Primary, 1200);
                    if (r.Status == IPStatus.Success && (best < 0 || r.RoundtripTime < best)) best = r.RoundtripTime;
                }
                p.Ping = best;
            }
            catch { p.Ping = -1; }
        });
        return list.OrderBy(x => x.Ping < 0 ? long.MaxValue : x.Ping).ToList();
    }

    public static Dictionary<string, object> TcpState() =>
        Cached.Get("net.tcp", TimeSpan.FromSeconds(20), ReadTcpState);

    private static Dictionary<string, object> ReadTcpState()
    {
        var result = new Dictionary<string, object>
        {
            ["autotuning"] = "",
            ["ecn"] = "",
            ["rss"] = "",
            ["rsc"] = "",
            ["timestamps"] = ""
        };

        var r = Sh.Ps("$s = Get-NetTCPSetting -SettingName Internet -ErrorAction SilentlyContinue; " +
                      "$g = Get-NetOffloadGlobalSetting -ErrorAction SilentlyContinue; " +
                      "\"{0}`t{1}`t{2}`t{3}`t{4}`t{5}\" -f $s.AutoTuningLevelLocal, $s.EcnCapability, $s.Timestamps, " +
                      "$s.ScalingHeuristics, $g.ReceiveSideScaling, $g.ReceiveSegmentCoalescing", 30000);

        if (!r.Ok) return result;

        var parts = r.Out.Trim().Split('	');
        if (parts.Length >= 4)
        {
            result["autotuning"] = parts[0].Trim();
            result["ecn"] = parts[1].Trim();
            result["timestamps"] = parts[2].Trim();
            result["heuristics"] = parts[3].Trim();
        }
        if (parts.Length >= 6)
        {
            result["rss"] = parts[4].Trim();
            result["rsc"] = parts[5].Trim();
        }
        return result;
    }

    public static string ResetStack()
    {
        var steps = new[]
        {
            ("netsh.exe", "winsock reset"),
            ("netsh.exe", "int ip reset"),
            ("netsh.exe", "int ipv6 reset"),
            ("ipconfig.exe", "/release"),
            ("ipconfig.exe", "/renew"),
            ("ipconfig.exe", "/flushdns"),
            ("arp.exe", "-d *"),
            ("nbtstat.exe", "-R")
        };
        var errors = new List<string>();
        foreach (var (exe, args) in steps)
        {
            var r = Sh.Run(exe, args, 60000);
            if (!r.Ok) errors.Add(Path.GetFileName(exe) + " " + args);
        }
        return errors.Count == 0 ? "ok" : "проблемы: " + string.Join(", ", errors);
    }

    public static List<object> PingTargets()
    {
        var targets = new (string Name, string Host)[]
        {
            ("Cloudflare", "1.1.1.1"), ("Google", "8.8.8.8"), ("Yandex", "77.88.8.8"),
            ("Steam", "steamcommunity.com"), ("Discord", "discord.com"), ("Klondaik", "klondaik.uk")
        };
        var res = new List<object>();
        Parallel.ForEach(targets, t =>
        {
            long ms = -1;
            double loss = 0;
            try
            {
                using var ping = new Ping();
                var times = new List<long>();
                int lost = 0;
                for (int i = 0; i < 4; i++)
                {
                    try
                    {
                        var r = ping.Send(t.Host, 2000);
                        if (r.Status == IPStatus.Success) times.Add(r.RoundtripTime);
                        else lost++;
                    }
                    catch { lost++; }
                }
                if (times.Count > 0) ms = (long)times.Average();
                loss = lost * 100.0 / 4;
            }
            catch { }
            lock (res) res.Add(new { name = t.Name, host = t.Host, ping = ms, loss });
        });
        return res;
    }

    public static bool TelemetryBlocked()
    {
        var cur = HostsFile.Current();
        return TelemetryHosts.Count(h => cur.Contains(h)) > TelemetryHosts.Length / 2;
    }
}
