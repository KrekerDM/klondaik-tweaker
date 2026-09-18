using System.Diagnostics;
using System.Text;
using System.Text.Json;
using KlondaikTweaker.Core.Engine;

namespace KlondaikTweaker.Host;

public static class SelfTest
{
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
                if (state == Core.Model.TweakState.Applied) applied++;
                if (state == Core.Model.TweakState.Unavailable) unavailable++;
            }
            return new { total = Catalog.Db.Tweaks.Count, applied, unavailable };
        });
        Check("wizard.questions", () => Api.Handle("wizard.questions", null, Noop));
        Check("wizard.resolve", () => Api.Handle("wizard.resolve", null, Noop));
        Check("services.list", () => ((IEnumerable<object>)Api.Handle("services.list", null, Noop)!).Count());
        Check("startup.list", () => Core.Modules.Startup.List().Count);
        Check("appx.list", () => Core.Modules.Appx.List(true).Count);
        Check("clean.scan", () => Core.Modules.Cleaner.Scan().Sum(x => x.Bytes));
        Check("disks", () => Core.Modules.Cleaner.DiskInfo());
        Check("net.adapters", () => Core.Modules.NetworkTools.Adapters().Count);
        Check("net.tcp", () => Core.Modules.NetworkTools.TcpState());
        Check("hosts", () => Core.Win.HostsFile.Current().Count);
        Check("restore.enabled", () => RestorePoint.Enabled());
        Check("restore.list", () => RestorePoint.List().Count);
        Check("journal", () => Journal.Entries.Count);
        Check("metrics", () => Core.Modules.HwMonitor.Read());
        Check("boot.time", () => Core.Modules.Benchmark.BootTime());
        Check("winget", () => Core.Modules.SoftCatalog.HasWinget());
        Check("tasks", () => Core.Win.Tasks.GetEnabled(@"\Microsoft\Windows\Defrag\ScheduledDefrag"));

        var failed = results.Count(x => x.GetType().GetProperty("ok")!.GetValue(x) is false);
        report.AppendLine();
        report.AppendLine($"{results.Count - failed}/{results.Count} passed");

        try
        {
            File.WriteAllText(outPath, report.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.ChangeExtension(outPath, ".json"), JsonSerializer.Serialize(results, Store.Options));
        }
        catch { }
    }
}
