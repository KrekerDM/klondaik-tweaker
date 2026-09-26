using System.Text.Json;
using System.Text.Json.Serialization;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Model;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class RepairDb
{
    [JsonPropertyName("serviceDefaults")] public Dictionary<string, string> ServiceDefaults { get; set; } = [];
    [JsonPropertyName("critical")] public List<string> Critical { get; set; } = [];
}

public sealed class RepairResult
{
    public bool Ok { get; set; } = true;
    public int Changed { get; set; }
    public int Skipped { get; set; }
    public string Message { get; set; } = "";
    public List<string> Details { get; set; } = [];
}

public static class Repair
{
    private static RepairDb? _db;
    private static readonly object Gate = new();

    public static RepairDb Db
    {
        get
        {
            lock (Gate)
            {
                if (_db is not null) return _db;
                var json = Res.Text("data/repair.json");
                _db = string.IsNullOrWhiteSpace(json)
                    ? new RepairDb()
                    : JsonSerializer.Deserialize<RepairDb>(json, Store.Options) ?? new RepairDb();
                return _db;
            }
        }
    }

    public static RepairResult RestoreServices()
    {
        var result = new RepairResult();
        var entry = new JournalEntry { TweakId = "repair.services", Title = Texts.Pick("Службы по умолчанию", "Default services"), Group = "repair" };

        foreach (var (name, mode) in Db.ServiceDefaults)
        {
            try
            {
                if (!Svc.Exists(name)) { result.Skipped++; continue; }
                var current = Svc.GetStart(name);
                if (string.Equals(current, mode, StringComparison.OrdinalIgnoreCase)) { result.Skipped++; continue; }
                entry.Items.Add(new JournalItem { Kind = "svc", Target = name, PrevValue = current });
                Svc.SetStart(name, mode);
                result.Changed++;
                if (result.Details.Count < 40) result.Details.Add($"{name}: {current} -> {mode}");
            }
            catch (Exception ex)
            {
                result.Skipped++;
                if (result.Details.Count < 40) result.Details.Add($"{name}: {Trim(ex.Message)}");
            }
        }

        if (entry.Items.Count > 0) Journal.Add(entry);
        result.Message = result.Changed == 0
            ? Texts.Pick("все службы уже в значениях по умолчанию", "every service already has its default value")
            : Texts.Pick($"возвращено служб: {result.Changed}", $"services restored: {result.Changed}");
        return result;
    }

    public static RepairResult RestoreDefender()
    {
        var result = new RepairResult();
        var entry = new JournalEntry { TweakId = "repair.defender", Title = Texts.Pick("Защитник Windows", "Windows Defender"), Group = "repair" };

        var policyKeys = new[]
        {
            (@"SOFTWARE\Policies\Microsoft\Windows Defender", new[] { "DisableAntiSpyware", "DisableAntiVirus", "DisableRoutinelyTakingAction", "ServiceKeepAlive" }),
            (@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection", new[] { "DisableRealtimeMonitoring", "DisableBehaviorMonitoring", "DisableScanOnRealtimeEnable", "DisableIOAVProtection", "DisableOnAccessProtection" }),
            (@"SOFTWARE\Policies\Microsoft\Windows Defender\Reporting", new[] { "DisableEnhancedNotifications" }),
            (@"SOFTWARE\Microsoft\Windows Defender", new[] { "DisableAntiSpyware", "DisableAntiVirus" }),
            (@"SOFTWARE\Microsoft\Windows Defender\Real-Time Protection", new[] { "DisableRealtimeMonitoring" })
        };

        foreach (var (path, names) in policyKeys)
        {
            foreach (var name in names)
            {
                try
                {
                    var prev = Reg.Read("HKLM", path, name);
                    if (!prev.Exists) continue;
                    entry.Items.Add(new JournalItem { Kind = "reg", Target = "HKLM\\" + path, Name = name, Existed = true, PrevType = prev.Kind, PrevValue = prev.Value });
                    Reg.DeleteValue("HKLM", path, name);
                    result.Changed++;
                }
                catch (Exception ex) { result.Details.Add($"{name}: {Trim(ex.Message)}"); }
            }
        }

        foreach (var (name, mode) in new[] { ("WinDefend", "auto"), ("SecurityHealthService", "manual"), ("wscsvc", "auto"), ("mpssvc", "auto"), ("WdNisSvc", "manual"), ("Sense", "manual"), ("BFE", "auto") })
        {
            try
            {
                if (!Svc.Exists(name)) continue;
                var current = Svc.GetStart(name);
                if (string.Equals(current, mode, StringComparison.OrdinalIgnoreCase)) continue;
                entry.Items.Add(new JournalItem { Kind = "svc", Target = name, PrevValue = current });
                Svc.SetStart(name, mode);
                result.Changed++;
                result.Details.Add($"{name}: {current} -> {mode}");
            }
            catch (Exception ex) { result.Details.Add($"{name}: {Trim(ex.Message)}"); }
        }

        if (entry.Items.Count > 0) Journal.Add(entry);
        result.Message = result.Changed == 0
            ? Texts.Pick("Защитник не был отключён", "Defender was not turned off")
            : Texts.Pick($"снято ограничений: {result.Changed}", $"restrictions lifted: {result.Changed}");
        return result;
    }

    public static RepairResult RestoreUpdates()
    {
        var result = new RepairResult();
        var entry = new JournalEntry { TweakId = "repair.updates", Title = Texts.Pick("Центр обновления", "Windows Update"), Group = "repair" };

        foreach (var path in new[] { @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU", @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate" })
        {
            foreach (var (name, _) in Reg.Values("HKLM", path))
            {
                try
                {
                    var prev = Reg.Read("HKLM", path, name);
                    if (!prev.Exists) continue;
                    entry.Items.Add(new JournalItem { Kind = "reg", Target = "HKLM\\" + path, Name = name, Existed = true, PrevType = prev.Kind, PrevValue = prev.Value });
                    Reg.DeleteValue("HKLM", path, name);
                    result.Changed++;
                }
                catch (Exception ex) { result.Details.Add($"{name}: {Trim(ex.Message)}"); }
            }
        }

        foreach (var (name, mode) in new[] { ("wuauserv", "manual"), ("UsoSvc", "manual"), ("WaaSMedicSvc", "manual"), ("DoSvc", "auto"), ("BITS", "manual"), ("TrustedInstaller", "manual") })
        {
            try
            {
                if (!Svc.Exists(name)) continue;
                var current = Svc.GetStart(name);
                if (string.Equals(current, mode, StringComparison.OrdinalIgnoreCase)) continue;
                entry.Items.Add(new JournalItem { Kind = "svc", Target = name, PrevValue = current });
                Svc.SetStart(name, mode);
                result.Changed++;
                result.Details.Add($"{name}: {current} -> {mode}");
            }
            catch (Exception ex) { result.Details.Add($"{name}: {Trim(ex.Message)}"); }
        }

        if (entry.Items.Count > 0) Journal.Add(entry);
        result.Message = result.Changed == 0
            ? Texts.Pick("обновления не были заблокированы", "updates were not blocked")
            : Texts.Pick($"снято ограничений: {result.Changed}", $"restrictions lifted: {result.Changed}");
        return result;
    }

    public static RepairResult ResetStore()
    {
        var result = new RepairResult();
        var steps = new List<string>();

        var reset = Sh.Run(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsreset.exe"), "-i", 120000);
        steps.Add("wsreset: " + (reset.Ok ? "ok" : reset.All));

        foreach (var (name, mode) in new[] { ("ClipSVC", "manual"), ("InstallService", "manual"), ("LicenseManager", "manual"), ("AppXSvc", "manual"), ("StateRepository", "manual"), ("TokenBroker", "manual"), ("wlidsvc", "manual") })
        {
            try
            {
                if (!Svc.Exists(name)) continue;
                if (Svc.GetStart(name) == "disabled")
                {
                    Svc.SetStart(name, mode);
                    result.Changed++;
                    steps.Add($"{name}: disabled -> {mode}");
                }
            }
            catch (Exception ex) { steps.Add($"{name}: {Trim(ex.Message)}"); }
        }

        var reregister = Sh.Ps("Get-AppxPackage -AllUsers *WindowsStore* | ForEach-Object { Add-AppxPackage -DisableDevelopmentMode -Register \"$($_.InstallLocation)\\AppXManifest.xml\" -ErrorAction SilentlyContinue }", 300000);
        steps.Add("re-register: " + (reregister.Ok ? "ok" : Trim(reregister.All)));

        result.Details = steps;
        result.Message = Texts.Pick("Store переустановлен, кэш сброшен", "Store re-registered, cache cleared");
        return result;
    }

    public static RepairResult RebuildSearch()
    {
        var result = new RepairResult();
        try
        {
            Reg.Write("HKLM", @"SOFTWARE\Microsoft\Windows Search", "SetupCompletedSuccessfully", "dword", "0");
            if (Svc.Exists("WSearch"))
            {
                Svc.SetStart("WSearch", "delayed");
                Svc.Stop("WSearch");
                Svc.Start("WSearch");
            }
            result.Changed = 1;
            result.Message = Texts.Pick("индекс поиска будет перестроен", "the search index will be rebuilt");
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Message = Trim(ex.Message);
        }
        return result;
    }

    public static RepairResult RestoreNetwork()
    {
        var result = new RepairResult();
        var steps = new List<string> { NetworkTools.ResetStack() };

        foreach (var (name, mode) in new[] { ("Dnscache", "auto"), ("Dhcp", "auto"), ("nsi", "auto"), ("netprofm", "manual"), ("NlaSvc", "auto"), ("iphlpsvc", "auto"), ("WlanSvc", "manual"), ("BFE", "auto"), ("mpssvc", "auto") })
        {
            try
            {
                if (!Svc.Exists(name)) continue;
                var current = Svc.GetStart(name);
                if (string.Equals(current, mode, StringComparison.OrdinalIgnoreCase)) continue;
                Svc.SetStart(name, mode);
                result.Changed++;
                steps.Add($"{name}: {current} -> {mode}");
            }
            catch (Exception ex) { steps.Add($"{name}: {Trim(ex.Message)}"); }
        }

        HostsFile.Unblock(NetworkTools.TelemetryHosts);
        result.Details = steps;
        result.Message = Texts.Pick("сетевой стек сброшен, нужна перезагрузка", "network stack reset, a restart is needed");
        return result;
    }

    public static RepairResult OpenGodMode()
    {
        var result = new RepairResult();
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "GodMode.{ED7BA470-8E54-465E-825C-99712043E01C}");
            Directory.CreateDirectory(dir);
            Sh.OpenExternal(dir);
            result.Message = Texts.Pick("панель всех настроек открыта", "the all-settings panel is open");
        }
        catch (Exception ex)
        {
            result.Ok = false;
            result.Message = Trim(ex.Message);
        }
        return result;
    }

    public static RepairResult FlushDns()
    {
        var r = Sh.Run("ipconfig.exe", "/flushdns", 20000);
        return new RepairResult { Ok = r.Ok, Message = r.Ok ? Texts.Pick("кэш DNS очищен", "DNS cache cleared") : Trim(r.All) };
    }

    private static string Trim(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 160 ? s[..160] : s;
    }
}
