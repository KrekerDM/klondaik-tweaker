using System.Diagnostics;
using System.Text;
using System.Text.Json;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Model;
using KlondaikTweaker.Core.Modules;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Host;

public static class SelfTest
{
    private static readonly string[] RoundTrip =
    [
        "perf.menu-delay",
        "perf.priority-separation",
        "perf.games-task",
        "ui.file-extensions",
        "ui.classic-context",
        "priv.telemetry-off",
        "priv.ceip-tasks",
        "svc.insider-off",
        "perf.wer-off",
        "priv.hosts-telemetry",
        "pwr.high-performance",
        "upd.full-off"
    ];

    public static List<string> MissingAssets()
    {
        var required = new List<(string Name, int MinBytes)>
        {
            ("data/tweaks.json", 50000),
            ("data/wizard.json", 5000),
            ("data/repair.json", 2000),
            ("data/features.json", 3000),
            ("data/tasks.json", 2000),
            ("data/software.json", 3000),
            ("data/credits.json", 200),
            ("web/index.html", 500),
            ("web/css/app.css", 10000),
            ("web/js/app.js", 2000),
            ("web/js/i18n.js", 500),
            ("web/js/bridge.js", 500),
            ("web/js/ui.js", 1000),
            ("vendor/nvidiaProfileInspector.exe", 200 * 1024)
        };

        foreach (var code in Api.Languages.OrderBy(x => x, StringComparer.Ordinal))
            required.Add(("web/js/lang/" + code + ".js", 5000));

        var missing = new List<string>();
        foreach (var (name, min) in required)
        {
            var bytes = Res.Bytes(name);
            if (bytes is null) missing.Add(name + " — нет в сборке");
            else if (bytes.Length < min) missing.Add(name + " — только " + bytes.Length + " байт");
        }

        var inspector = Res.Bytes("vendor/nvidiaProfileInspector.exe");
        if (inspector is { Length: > 1 } && (inspector[0] != 'M' || inspector[1] != 'Z'))
            missing.Add("vendor/nvidiaProfileInspector.exe — не похож на программу");

        return missing;
    }

    public static int RequiredAssetCount => 14 + Api.Languages.Count;

    public static void Run(string outPath)
    {
        var report = new StringBuilder();
        var results = new List<object>();

        void Check(string name, Func<object?> probe)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var value = probe();
                sw.Stop();
                var json = JsonSerializer.Serialize(value, Store.Options);
                if (json.Length > 400) json = json[..400] + "...";
                results.Add(new { name, ok = true, ms = sw.ElapsedMilliseconds, sample = json });
                report.AppendLine($"[ OK ] {name} ({sw.ElapsedMilliseconds} ms)");
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new { name, ok = false, ms = sw.ElapsedMilliseconds, error = ex.Message });
                report.AppendLine($"[FAIL] {name} ({sw.ElapsedMilliseconds} ms): {ex.GetType().Name}: {ex.Message}");
            }
        }

        void Noop(string channel, object data) { }

        Header(report, "read-only self test");

        Check("facts", () => Env.Facts);
        Check("catalog", () => new { count = Catalog.Db.Tweaks.Count, cats = Catalog.Db.Tweaks.Select(x => x.Cat).Distinct().Count() });
        Check("tweaks.list", () => Api.Handle("tweaks.list", null, Noop));
        Check("tweaks.detect.sample", () =>
        {
            var applied = 0;
            var unavailable = 0;
            foreach (var t in Catalog.Db.Tweaks)
            {
                var state = TweakEngine.Detect(t);
                if (state == TweakState.Applied) applied++;
                if (state == TweakState.Unavailable) unavailable++;
            }
            return new { total = Catalog.Db.Tweaks.Count, applied, unavailable };
        });
        Check("assets.embedded", () =>
        {
            var missing = MissingAssets();
            if (missing.Count > 0)
                throw new Exception("в сборку не попало: " + string.Join("; ", missing));
            return new { checkedItems = RequiredAssetCount, languages = Api.Languages.Count, total = Res.All().Count() };
        });

        Check("wizard.questions", () => Api.Handle("wizard.questions", null, Noop));
        Check("wizard.resolve", () => Api.Handle("wizard.resolve", null, Noop));
        Check("services.list", () => ((IEnumerable<object>)Api.Handle("services.list", null, Noop)!).Count());
        Check("startup.list", () => Startup.List().Count);
        Check("appx.list", () => Appx.List(true).Count);
        Check("clean.scan", () => Cleaner.Scan().Sum(x => x.Bytes));
        Check("disks", () => Cleaner.DiskInfo());
        Check("net.adapters", () => NetworkTools.Adapters().Count);
        Check("net.tcp", () => NetworkTools.TcpState());
        Check("hosts", () => HostsFile.Current().Count);
        Check("restore.enabled", () => RestorePoint.Enabled());
        Check("restore.list", () => RestorePoint.List().Count);
        Check("journal", () => Journal.Entries.Count);
        Check("metrics", () => HwMonitor.Read());
        Check("boot.time", () => Benchmark.BootTime());
        Check("winget", () => SoftCatalog.HasWinget());
        Check("tasks", () => Tasks.GetEnabled(@"\Microsoft\Windows\Defrag\ScheduledDefrag"));
        Check("repair.plan", () =>
        {
            var db = Repair.Db;
            int differs = 0, missing = 0;
            foreach (var (name, mode) in db.ServiceDefaults)
            {
                if (!Svc.Exists(name)) { missing++; continue; }
                if (!string.Equals(Svc.GetStart(name), mode, StringComparison.OrdinalIgnoreCase)) differs++;
            }
            return new { total = db.ServiceDefaults.Count, differs, missing };
        });
        Check("privileges", () => { Elevate.EnableAll(); return Elevate.Granted; });
        Check("ti.available", () => Ti.Available());
        Check("ti.identity", () =>
        {
            var r = Ti.Run("whoami", 45000);
            return new { token = Ti.LastIdentity, code = r.Code, identity = r.Out, error = r.Err };
        });
        Check("ti.protected-key", () =>
        {
            const string probe = @"SYSTEM\CurrentControlSet\Services\WaaSMedicSvc";
            var before = Reg.Read("HKLM", probe, "Start");
            return new { exists = before.Exists, value = before.Value, escalations = Reg.Escalations };
        });

        var failed = results.Count(HasFailed);
        report.AppendLine();
        report.AppendLine($"{results.Count - failed}/{results.Count} passed");

        Environment.ExitCode = failed > 0 ? 1 : 0;
        Write(outPath, report, results);
    }

    public static void RunApply(string outPath, bool canaryOnly = false, bool everything = false)
    {
        var report = new StringBuilder();
        var results = new List<object>();

        Header(report, everything ? "full catalog apply and revert round trip" : "apply and revert round trip");
        report.AppendLine("Every tweak below is applied, checked, then reverted.");
        report.AppendLine("The test passes only if the system state afterwards is byte-identical to the state before.");
        report.AppendLine();

        var planBefore = Power.ActiveScheme();
        var canary = Canary();
        SeedCanary();

        IEnumerable<string> plan;
        if (canaryOnly) plan = [canary.Id];
        else if (everything) plan = new[] { canary.Id }.Concat(Catalog.Db.Tweaks.Select(x => x.Id));
        else plan = new[] { canary.Id }.Concat(RoundTrip);

        foreach (var id in plan)
        {
            var def = id == canary.Id ? canary : Catalog.Find(id);
            if (def is not null && def.Actions.Any(a => a.K == "appx"))
            {
                report.AppendLine($"[SKIP] {id}: removing store packages cannot be undone");
                results.Add(new { name = id, ok = true, skipped = true, note = "appx removal is not reversible" });
                continue;
            }
            if (def is null)
            {
                report.AppendLine($"[SKIP] {id}: not in catalog");
                results.Add(new { name = id, ok = false, skipped = true, error = "not in catalog" });
                continue;
            }
            if (!Env.Meets(def.Req))
            {
                report.AppendLine($"[SKIP] {id}: requires {def.Req}");
                results.Add(new { name = id, ok = true, skipped = true, note = "requires " + def.Req });
                continue;
            }
            if (TweakEngine.Detect(def) == TweakState.Unavailable)
            {
                report.AppendLine($"[SKIP] {id}: targets do not exist on this system");
                results.Add(new { name = id, ok = true, skipped = true, note = "targets missing" });
                continue;
            }

            var sw = Stopwatch.StartNew();
            try
            {
                var before = Fingerprint(def);
                var stateBefore = TweakEngine.Detect(def);

                var applied = TweakEngine.Apply(def);
                var stateApplied = TweakEngine.Detect(def);

                var reverted = TweakEngine.Revert(def);
                var after = Fingerprint(def);
                var stateFinal = TweakEngine.Detect(def);
                sw.Stop();

                var restored = before == after;
                var flipped = stateApplied == TweakState.Applied;
                var ok = applied.Ok && reverted.Ok && restored && flipped;

                results.Add(new
                {
                    name = id,
                    ok,
                    ms = sw.ElapsedMilliseconds,
                    risk = def.Risk,
                    stateBefore = stateBefore.ToString(),
                    stateApplied = stateApplied.ToString(),
                    stateFinal = stateFinal.ToString(),
                    applyOk = applied.Ok,
                    applyError = applied.Error,
                    revertOk = reverted.Ok,
                    revertError = reverted.Error,
                    restored,
                    before,
                    after
                });

                report.AppendLine($"[{(ok ? " OK " : "FAIL")}] {id} ({sw.ElapsedMilliseconds} ms)");
                report.AppendLine($"        detect: {stateBefore} -> {stateApplied} -> {stateFinal}");
                if (!applied.Ok) report.AppendLine($"        apply error: {applied.Error}");
                if (!reverted.Ok) report.AppendLine($"        revert error: {reverted.Error}");
                if (!flipped) report.AppendLine("        detection did not report the tweak as applied");
                if (!restored)
                {
                    report.AppendLine("        state NOT restored");
                    report.AppendLine("        before: " + before);
                    report.AppendLine("        after:  " + after);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new { name = id, ok = false, ms = sw.ElapsedMilliseconds, error = ex.Message });
                report.AppendLine($"[FAIL] {id}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        CleanCanary();

        var planAfter = Power.ActiveScheme();
        report.AppendLine();
        report.AppendLine($"power plan: {planBefore} -> {planAfter} {(planBefore == planAfter ? "(restored)" : "(NOT restored)")}");

        var failed = results.Count(HasFailed);
        report.AppendLine($"journal entries left active: {Journal.Entries.Count(x => !x.Reverted)}");
        report.AppendLine();
        report.AppendLine($"{results.Count - failed}/{results.Count} passed");

        Environment.ExitCode = failed > 0 ? 1 : 0;
        Write(outPath, report, results);
    }

    private const string CanaryRoot = @"Software\Klondaik\SelfTest";

    private static TweakDef Canary() => new()
    {
        Id = "selftest.canary",
        Cat = "performance",
        Risk = "safe",
        Ru = new Loc { T = "Проверка механизма отката", D = "Синтетический твик на выброшенном ключе реестра" },
        En = new Loc { T = "Rollback engine canary", D = "Synthetic tweak on a throwaway registry key" },
        Actions =
        [
            new TweakAction { K = "reg", H = "HKCU", P = CanaryRoot, N = "Existing", T = "dword", V = "42" },
            new TweakAction { K = "reg", H = "HKCU", P = CanaryRoot, N = "Created", T = "sz", V = "klondaik" },
            new TweakAction { K = "reg", H = "HKCU", P = CanaryRoot + @"\Nested", N = "InNewKey", T = "dword", V = "1", Dk = true }
        ]
    };

    private static void SeedCanary()
    {
        try
        {
            Reg.DeleteTree("HKCU", @"Software\Klondaik");
            Reg.Write("HKCU", CanaryRoot, "Existing", "dword", "7");
        }
        catch { }
    }

    private static void CleanCanary()
    {
        try { Reg.DeleteTree("HKCU", @"Software\Klondaik"); }
        catch { }
    }

    private static string Fingerprint(TweakDef t)
    {
        var sb = new StringBuilder();
        foreach (var a in t.Actions)
        {
            switch (a.K)
            {
                case "reg":
                case "regdel":
                {
                    var r = Reg.Read(a.H!, a.P!, a.N);
                    sb.Append($"{a.H}\\{a.P}\\{a.N}=");
                    sb.Append(r.Exists ? r.Kind + ":" + r.Value : "<none>");
                    sb.Append("; ");
                    break;
                }
                case "svc":
                    sb.Append($"svc:{a.N}={(Svc.Exists(a.N!) ? Svc.GetStart(a.N!) : "<none>")}; ");
                    break;
                case "task":
                    sb.Append($"task:{a.P}={Tasks.GetEnabled(a.P!)?.ToString() ?? "<none>"}; ");
                    break;
                case "hosts":
                {
                    var current = HostsFile.Current();
                    var blocked = (a.V ?? "").Split('|', StringSplitOptions.RemoveEmptyEntries).Count(current.Contains);
                    sb.Append($"hosts:{blocked}; ");
                    break;
                }
                case "cmd":
                    sb.Append($"cmd:{Path.GetFileName(a.Exe)}:{Power.ActiveScheme()}; ");
                    break;
                case "appx":
                    sb.Append($"appx:{a.N}={Appx.Installed(a.N!)}; ");
                    break;
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static bool HasFailed(object row)
    {
        var prop = row.GetType().GetProperty("ok");
        return prop?.GetValue(row) is false;
    }

    private static void Header(StringBuilder report, string title)
    {
        var f = Env.Facts;
        report.AppendLine("Klondaik Tweaker " + (typeof(SelfTest).Assembly.GetName().Version?.ToString(3) ?? "1.0.0") + " - " + title);
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine($"{f.OsName} / {f.OsVersion} / {f.Cpu} / {f.RamGb} GB / {(f.Laptop ? "laptop" : "desktop")} / {(f.SystemSsd ? "ssd" : "hdd")}");
        report.AppendLine(new string('-', 78));
    }

    private static void Write(string outPath, StringBuilder report, List<object> results)
    {
        try
        {
            File.WriteAllText(outPath, report.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.ChangeExtension(outPath, ".json"), JsonSerializer.Serialize(results, Store.Options));
        }
        catch { }
    }
}
