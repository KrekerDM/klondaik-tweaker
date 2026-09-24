using System.Text;
using KlondaikTweaker.Core.Modules;

namespace KlondaikTweaker.Host;

public static class NicDump
{
    public static void Run(string outPath)
    {
        var report = new StringBuilder();
        report.AppendLine("Klondaik Tweaker - network adapter parameters");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine(new string('-', 78));

        var adapters = Nic.Adapters();
        if (adapters.Count == 0) report.AppendLine("адаптеров с настраиваемыми параметрами не найдено");

        foreach (var adapter in adapters)
        {
            var list = Nic.Params(adapter.Id);
            report.AppendLine();
            report.AppendLine($"=== [{adapter.Id}] {adapter.Name} ===");
            report.AppendLine($"параметров: {list.Count}, изменено от заводских: {list.Count(x => x.Edited)}");

            foreach (var p in list)
            {
                var flag = p.Edited ? "ИЗМЕНЁН" : "по умолч";
                var risky = p.Risky ? " [осторожно]" : "";
                report.AppendLine($"  {flag}{risky}  {p.Desc}");
                report.AppendLine($"            {p.Name}  тип={p.Type}  сейчас={Show(p.Current, p)}  заводское={Show(p.Default, p)}");
                if (p.Options.Count > 0)
                    report.AppendLine("            варианты: " + string.Join(", ", p.Options.Select(o => $"{o.Value}={o.Text}")));
                else if (p.Min is not null || p.Max is not null)
                    report.AppendLine($"            диапазон: {p.Min ?? "?"} .. {p.Max ?? "?"}");
            }
        }

        var text = report.ToString();
        try { File.WriteAllText(outPath, text, new UTF8Encoding(false)); }
        catch { }
        Console.WriteLine(text);
    }

    private static string Show(string value, NicParam p)
    {
        var hit = p.Options.FirstOrDefault(x => x.Value.Equals(value, StringComparison.OrdinalIgnoreCase));
        return hit is null ? value : $"{value} ({hit.Text})";
    }
}
