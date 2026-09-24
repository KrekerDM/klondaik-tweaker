using System.Text;
using KlondaikTweaker.Core.Modules;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Host;

public static class IrqDump
{
    public static void Run(string outPath)
    {
        var report = new StringBuilder();

        var threads = Cpu.Threads();
        report.AppendLine("Klondaik Tweaker - interrupt affinity dump");
        report.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        report.AppendLine(new string('-', 78));

        report.AppendLine($"потоков: {threads.Count}, гибридный процессор: {(Cpu.Hybrid() ? "да" : "нет")}");
        foreach (var group in threads.GroupBy(x => x.Core).OrderBy(x => x.Key))
        {
            var kind = group.First().Performance ? "P-Core" : "E-Core";
            var list = string.Join(", ", group.Select(x => x.Index));
            report.AppendLine($"  ядро {group.Key,2}  {kind}  потоки: {list}  efficiency={group.First().Efficiency}");
        }

        report.AppendLine();
        report.AppendLine("устройства:");

        var devices = Irq.Devices();
        if (devices.Count == 0) report.AppendLine("  ничего не найдено");

        foreach (var d in devices)
        {
            var bound = d.Bound ? $"привязано к маске 0x{d.Mask:X}" : "не привязано";
            report.AppendLine($"  [{d.Kind,-7}] {d.Name}");
            report.AppendLine($"            {bound}, policy={d.Policy}, priority={d.Priority}, служба={d.Service}");
            report.AppendLine($"            {d.Id}");
        }

        report.AppendLine();
        report.AppendLine($"всего устройств: {devices.Count}");

        var text = report.ToString();
        try { File.WriteAllText(outPath, text, new UTF8Encoding(false)); }
        catch { }
        Console.WriteLine(text);
    }
}
