using System.Diagnostics;
using System.Security.Cryptography;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class BenchResult
{
    public DateTime Utc { get; set; } = DateTime.UtcNow;
    public string Label { get; set; } = "";
    public double CpuSingle { get; set; }
    public double CpuMulti { get; set; }
    public double RamGbs { get; set; }
    public double DiskWrite { get; set; }
    public double DiskRead { get; set; }
    public double LatencyAvg { get; set; }
    public double LatencyMax { get; set; }
    public double BootSeconds { get; set; }
    public int Score { get; set; }
}

public sealed class BenchFile
{
    public List<BenchResult> Runs { get; set; } = [];
}

public static class Benchmark
{
    public static event Action<string, int>? Progress;

    private static void Step(string stage, int percent) => Progress?.Invoke(stage, percent);

    public static BenchResult Run(string label, bool includeDisk = true)
    {
        var r = new BenchResult { Label = label };

        Step("cpu-single", 5);
        r.CpuSingle = Math.Round(CpuScore(1), 0);

        Step("cpu-multi", 25);
        r.CpuMulti = Math.Round(CpuScore(Environment.ProcessorCount), 0);

        Step("ram", 45);
        r.RamGbs = Math.Round(RamBandwidth(), 2);

        if (includeDisk)
        {
            Step("disk", 60);
            var (w, rd) = DiskSpeed();
            r.DiskWrite = Math.Round(w, 0);
            r.DiskRead = Math.Round(rd, 0);
        }

        Step("latency", 85);
        var (avg, max) = TimerJitter();
        r.LatencyAvg = Math.Round(avg, 3);
        r.LatencyMax = Math.Round(max, 3);

        Step("boot", 95);
        r.BootSeconds = BootTime();

        r.Score = (int)Math.Round(
            r.CpuSingle * 0.22 +
            r.CpuMulti * 0.05 +
            r.RamGbs * 120 +
            (r.DiskWrite + r.DiskRead) * 0.35 +
            Math.Max(0, 600 - r.LatencyAvg * 400));

        Step("done", 100);
        Save(r);
        return r;
    }

    private static double CpuScore(int threads)
    {
        var sw = Stopwatch.StartNew();
        long total = 0;
        var duration = TimeSpan.FromMilliseconds(threads == 1 ? 1600 : 1600);
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
        {
            var data = new byte[4096];
            Random.Shared.NextBytes(data);
            long local = 0;
            using var sha = SHA256.Create();
            var buf = new byte[32];
            while (sw.Elapsed < duration)
            {
                for (int i = 0; i < 64; i++)
                {
                    sha.TryComputeHash(data, buf, out _);
                    data[0] = buf[0];
                }
                local += 64;
            }
            Interlocked.Add(ref total, local);
        });
        sw.Stop();
        return total / sw.Elapsed.TotalSeconds / 100.0;
    }

    private static double RamBandwidth()
    {
        const int size = 64 * 1024 * 1024;
        var a = new byte[size];
        var b = new byte[size];
        Random.Shared.NextBytes(a);
        var sw = Stopwatch.StartNew();
        int rounds = 0;
        while (sw.ElapsedMilliseconds < 1200)
        {
            Buffer.BlockCopy(a, 0, b, 0, size);
            rounds++;
        }
        sw.Stop();
        var bytes = (double)size * rounds * 2;
        return bytes / sw.Elapsed.TotalSeconds / 1024 / 1024 / 1024;
    }

    private static (double Write, double Read) DiskSpeed()
    {
        var path = Path.Combine(Paths.Root, "bench.tmp");
        const int chunk = 8 * 1024 * 1024;
        const int chunks = 24;
        var buffer = new byte[chunk];
        Random.Shared.NextBytes(buffer);
        double write = 0, read = 0;
        try
        {
            var sw = Stopwatch.StartNew();
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, chunk, FileOptions.WriteThrough))
            {
                for (int i = 0; i < chunks; i++) fs.Write(buffer, 0, chunk);
                fs.Flush(true);
            }
            sw.Stop();
            write = (double)chunk * chunks / 1024 / 1024 / sw.Elapsed.TotalSeconds;

            sw.Restart();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, chunk, (FileOptions)0x20000000))
            {
                int n;
                while ((n = fs.Read(buffer, 0, chunk)) > 0) { }
            }
            sw.Stop();
            read = (double)chunk * chunks / 1024 / 1024 / sw.Elapsed.TotalSeconds;
        }
        catch { }
        finally { try { File.Delete(path); } catch { } }
        return (write, read);
    }

    private static (double Avg, double Max) TimerJitter()
    {
        var samples = new List<double>();
        var sw = new Stopwatch();
        for (int i = 0; i < 240; i++)
        {
            sw.Restart();
            Thread.Sleep(1);
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
        }
        samples.Sort();
        var trimmed = samples.Take(samples.Count - 8).ToList();
        return (trimmed.Average(), samples[^1]);
    }

    public static double BootTime()
    {
        try
        {
            var r = Sh.Ps("(Get-WinEvent -FilterHashtable @{LogName='Microsoft-Windows-Diagnostics-Performance/Operational'; Id=100} -MaxEvents 1 -ErrorAction Stop).Properties[7].Value", 25000);
            if (r.Ok && double.TryParse(r.Out.Trim(), out var ms) && ms > 0) return Math.Round(ms / 1000.0, 1);
        }
        catch { }
        return 0;
    }

    public static void Save(BenchResult r)
    {
        var f = Store.Load(Paths.Bench, () => new BenchFile());
        f.Runs.Add(r);
        if (f.Runs.Count > 40) f.Runs.RemoveRange(0, f.Runs.Count - 40);
        Store.Save(Paths.Bench, f);
    }

    public static List<BenchResult> History() => Store.Load(Paths.Bench, () => new BenchFile()).Runs.OrderByDescending(x => x.Utc).ToList();

    public static void ClearHistory() => Store.Save(Paths.Bench, new BenchFile());
}
