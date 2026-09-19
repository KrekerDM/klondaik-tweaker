using System.Diagnostics;
using System.Text;
using System.Text.Json;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Modules;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Host;

public static class ModuleTest
{
    public static void Run(string outPath)
    {
        var report = new StringBuilder();
        var results = new List<object>();
        var f = Env.Facts;

        report.AppendLine("Klondaik Tweaker " + (typeof(ModuleTest).Assembly.GetName().Version?.ToString(3) ?? "1.0.0") + " - module execution test");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine($"{f.OsName} / {f.OsVersion} / {f.RamGb} GB / tier={f.Tier}");
        report.AppendLine("Every module below is actually executed. Side effects are real.");
        report.AppendLine(new string('-', 78));

        void Flush()
        {
            try
            {
                File.WriteAllText(outPath, report.ToString(), new UTF8Encoding(false));
                File.WriteAllText(Path.ChangeExtension(outPath, ".json"), JsonSerializer.Serialize(results, Store.Options));
            }
            catch { }
        }

        void Step(string name, Func<object?> action, string? note = null)
        {
            report.AppendLine($"[ .. ] {name} started");
            Flush();
            var sw = Stopwatch.StartNew();
            try
            {
                var value = action();
                sw.Stop();
                var json = JsonSerializer.Serialize(value, Store.Options);
                if (json.Length > 300) json = json[..300] + "...";
                results.Add(new { name, ok = true, ms = sw.ElapsedMilliseconds, result = json });
                report.Remove(report.Length - ($"[ .. ] {name} started" + Environment.NewLine).Length, ($"[ .. ] {name} started" + Environment.NewLine).Length);
                report.AppendLine($"[ OK ] {name} ({sw.ElapsedMilliseconds} ms)");
                report.AppendLine("        " + json);
                if (note is not null) report.AppendLine("        " + note);
                Flush();
            }
            catch (Exception ex)
            {
                sw.Stop();
                results.Add(new { name, ok = false, ms = sw.ElapsedMilliseconds, error = ex.Message });
                report.Remove(report.Length - ($"[ .. ] {name} started" + Environment.NewLine).Length, ($"[ .. ] {name} started" + Environment.NewLine).Length);
                report.AppendLine($"[FAIL] {name} ({sw.ElapsedMilliseconds} ms): {ex.GetType().Name}: {ex.Message}");
                Flush();
            }
        }

        void Noop(string channel, object data) { }

        Step("monitor.trim", () => { var t = HwMonitor.TrimMemory(); return new { t.Trimmed, freedMb = t.Freed / 1024 / 1024 }; });
        Step("monitor.top", () => HwMonitor.TopProcesses(5).Count);
        Step("repair.dns", () => Repair.FlushDns());
        Step("repair.godmode", () => Repair.OpenGodMode());

        Step("restore.enable", () => new { result = RestorePoint.Enable(), enabled = RestorePoint.Enabled() });
        Step("restore.create", () => RestorePoint.Create("Klondaik module test"));
        Step("restore.list", () => RestorePoint.List().Count);

        Step("clean.scan", () =>
        {
            var targets = Cleaner.Scan();
            return new { count = targets.Count, totalMb = targets.Sum(x => x.Bytes) / 1024 / 1024 };
        });
        Step("clean.run", () =>
        {
            var ids = new[] { "temp.user", "temp.win", "logs", "crash", "thumbs" };
            var r = Cleaner.Clean(ids);
            return new { freedMb = r.Freed / 1024 / 1024, r.Files, errors = r.Errors.Count };
        });

        Step("bench.run", () =>
        {
            var b = Benchmark.Run("module test", includeDisk: true);
            return new { b.Score, b.CpuSingle, b.CpuMulti, b.RamGbs, b.DiskWrite, b.DiskRead, b.LatencyAvg };
        });
        Step("bench.history", () => Benchmark.History().Count);

        Step("boost.start", () =>
        {
            var st = GameBoost.Start();
            return new { st.Active, services = st.Services.Count, freedMb = st.FreedRam / 1024 / 1024 };
        });
        Step("boost.stop", () => new { GameBoost.Stop().Active });

        Step("appx.list", () => Appx.List(true).Count);
        Step("appx.remove", () =>
        {
            var target = Appx.List().FirstOrDefault(x => x.Name.Contains("BingWeather", StringComparison.OrdinalIgnoreCase)
                                                     || x.Name.Contains("BingNews", StringComparison.OrdinalIgnoreCase)
                                                     || x.Name.Contains("MicrosoftSolitaire", StringComparison.OrdinalIgnoreCase));
            if (target is null) return new { skipped = true, reason = "no removable sample package present" };
            var before = Appx.List().Count;
            var result = Appx.Remove(target.Name);
            var after = Appx.List(true).Count;
            return new { package = target.Name, result, before, after };
        });

        Step("repair.defender", () => Repair.RestoreDefender());
        Step("repair.updates", () => Repair.RestoreUpdates());
        Step("repair.search", () => Repair.RebuildSearch());
        Step("repair.services", () => { var r = Repair.RestoreServices(); return new { r.Changed, r.Skipped, r.Message }; });
        Step("repair.store", () => { var r = Repair.ResetStore(); return new { r.Changed, r.Message, details = r.Details.Count }; });

        Step("soft.winget", () => SoftCatalog.HasWinget());
        Step("api.router", () =>
        {
            var probes = new[] { "app.info", "tweaks.presets", "services.list", "startup.list", "net.adapters", "journal.list", "app.credits" };
            var ok = 0;
            foreach (var m in probes)
            {
                try { if (Api.Handle(m, null, Noop) is not null) ok++; }
                catch { }
            }
            return new { probed = probes.Length, ok };
        });

        Step("repair.network", () => { var r = Repair.RestoreNetwork(); return new { r.Changed, r.Message }; }, "network stack reset runs last");

        var failed = results.Count(x => x.GetType().GetProperty("ok")?.GetValue(x) is false);
        report.AppendLine();
        report.AppendLine($"journal entries left active: {Journal.Entries.Count(x => !x.Reverted)}");
        report.AppendLine($"{results.Count - failed}/{results.Count} passed");

        try
        {
            File.WriteAllText(outPath, report.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.ChangeExtension(outPath, ".json"), JsonSerializer.Serialize(results, Store.Options));
        }
        catch { }
    }
}
