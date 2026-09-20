using System.Diagnostics;
using System.Text;
using System.Text.Json;
using KlondaikTweaker.Core.Engine;

namespace KlondaikTweaker.Host;

public static class PerfTest
{
    private static readonly (string Method, string Payload)[] Calls =
    [
        ("app.info", "{}"),
        ("app.credits", "{}"),
        ("tweaks.presets", "{}"),
        ("tweaks.list", "{\"cat\":\"all\",\"risk\":\"all\"}"),
        ("wizard.questions", "{}"),
        ("journal.list", "{}"),
        ("repair.status", "{}"),
        ("restore.status", "{}"),
        ("monitor.read", "{}"),
        ("monitor.top", "{}"),
        ("startup.list", "{}"),
        ("services.list", "{}"),
        ("appx.list", "{}"),
        ("net.adapters", "{}"),
        ("net.tcp", "{}"),
        ("clean.scan", "{}"),
        ("soft.list", "{}")
    ];

    public static void Run(string outPath)
    {
        var report = new StringBuilder();
        var rows = new List<object>();
        var f = Env.Facts;

        report.AppendLine("Klondaik Tweaker " + (typeof(PerfTest).Assembly.GetName().Version?.ToString(3) ?? "1.0.0") + " - response times");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine($"{f.OsName} / {f.OsVersion} / {f.Threads} потоков / {f.RamGb} ГБ / tier={f.Tier}");
        report.AppendLine("Только чтение. Каждый вызов делается дважды: холодный и повторный.");
        report.AppendLine(new string('-', 78));
        report.AppendLine($"{"метод",-22}{"холодный",12}{"повторный",12}  оценка");

        void Noop(string channel, object data) { }

        foreach (var (method, payload) in Calls)
        {
            long cold = -1, warm = -1;
            string note = "";
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var p = doc.RootElement.Clone();

                var sw = Stopwatch.StartNew();
                Api.Handle(method, p, Noop);
                sw.Stop();
                cold = sw.ElapsedMilliseconds;

                sw.Restart();
                Api.Handle(method, p, Noop);
                sw.Stop();
                warm = sw.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                note = ex.GetType().Name + ": " + Short(ex.Message);
            }

            var verdict = note.Length > 0 ? "ОШИБКА" : Verdict(Math.Max(cold, warm));
            rows.Add(new { method, cold, warm, verdict, note });
            report.AppendLine($"{method,-22}{Ms(cold),12}{Ms(warm),12}  {verdict}{(note.Length > 0 ? " " + note : "")}");
        }

        report.AppendLine(new string('-', 78));
        report.AppendLine("норма до 150 мс, терпимо до 600 мс, дальше пользователь это замечает");

        try
        {
            File.WriteAllText(outPath, report.ToString(), new UTF8Encoding(false));
            File.WriteAllText(Path.ChangeExtension(outPath, ".json"), JsonSerializer.Serialize(rows, Store.Options));
        }
        catch { }

        Console.WriteLine(report.ToString());
    }

    private static string Ms(long v) => v < 0 ? "-" : v + " мс";

    private static string Verdict(long ms) => ms switch
    {
        < 150 => "ok",
        < 600 => "терпимо",
        < 2000 => "МЕДЛЕННО",
        _ => "ОЧЕНЬ МЕДЛЕННО"
    };

    private static string Short(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 90 ? s[..90] : s;
    }
}
