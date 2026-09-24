using System.Diagnostics;
using System.Security.Principal;
using KlondaikTweaker.Core.Engine;
using Microsoft.Web.WebView2.Core;

namespace KlondaikTweaker;

internal static class Program
{
    private static Mutex? _mutex;

    [STAThread]
    private static void Main()
    {
        _mutex = new Mutex(true, "Global\\KlondaikTweakerSingleton", out var created);
        if (!created)
        {
            MessageBox.Show("Klondaik Tweaker уже запущен.", "Klondaik Tweaker", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) => Crash(e.ExceptionObject as Exception);
        Application.ThreadException += (_, e) => Crash(e.Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { Log(e.Exception); e.SetObserved(); };

        Core.Win.Elevate.EnableAll();

        var args = Environment.GetCommandLineArgs();

        var nic = Array.FindIndex(args, a => a.Equals("--nic", StringComparison.OrdinalIgnoreCase));
        if (nic >= 0)
        {
            var target = nic + 1 < args.Length && !args[nic + 1].StartsWith("--")
                ? args[nic + 1]
                : Path.Combine(Paths.Root, "nic.txt");
            Host.NicDump.Run(target);
            return;
        }

        var irq = Array.FindIndex(args, a => a.Equals("--irq", StringComparison.OrdinalIgnoreCase));
        if (irq >= 0)
        {
            var target = irq + 1 < args.Length && !args[irq + 1].StartsWith("--")
                ? args[irq + 1]
                : Path.Combine(Paths.Root, "irq.txt");
            Host.IrqDump.Run(target);
            return;
        }

        var perf = Array.FindIndex(args, a => a.Equals("--perf", StringComparison.OrdinalIgnoreCase));
        if (perf >= 0)
        {
            var target = perf + 1 < args.Length && !args[perf + 1].StartsWith("--")
                ? args[perf + 1]
                : Path.Combine(Paths.Root, "perf.txt");
            Host.PerfTest.Run(target);
            return;
        }

        var runAsTi = Array.FindIndex(args, a => a.Equals("--runas-ti", StringComparison.OrdinalIgnoreCase));
        if (runAsTi >= 0)
        {
            var target = runAsTi + 1 < args.Length ? args[runAsTi + 1] : "";
            Environment.ExitCode = Core.Modules.ShellMenu.RunAsTi(target);
            return;
        }

        var takeOwn = Array.FindIndex(args, a => a.Equals("--take-ownership", StringComparison.OrdinalIgnoreCase));
        if (takeOwn >= 0)
        {
            var target = takeOwn + 1 < args.Length ? args[takeOwn + 1] : "";
            Environment.ExitCode = Core.Modules.ShellMenu.TakeOwnership(target);
            return;
        }

        var selfTest = Array.FindIndex(args, a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase));
        if (selfTest >= 0)
        {
            var target = selfTest + 1 < args.Length ? args[selfTest + 1] : Path.Combine(Paths.Root, "selftest.txt");
            Host.SelfTest.Run(target);
            return;
        }

        var moduleTest = Array.FindIndex(args, a => a.Equals("--selftest-modules", StringComparison.OrdinalIgnoreCase));
        if (moduleTest >= 0)
        {
            if (!IsAdmin())
            {
                MessageBox.Show("Тесту модулей нужны права администратора.", "Klondaik Tweaker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var target = moduleTest + 1 < args.Length && !args[moduleTest + 1].StartsWith("--")
                ? args[moduleTest + 1]
                : Path.Combine(Paths.Root, "selftest-modules.txt");
            Host.ModuleTest.Run(target);
            return;
        }

        var applyTest = Array.FindIndex(args, a => a.Equals("--selftest-apply", StringComparison.OrdinalIgnoreCase));
        if (applyTest >= 0)
        {
            var canaryOnly = args.Any(a => a.Equals("--canary-only", StringComparison.OrdinalIgnoreCase));
            var everything = args.Any(a => a.Equals("--all", StringComparison.OrdinalIgnoreCase));
            if (!canaryOnly && !IsAdmin())
            {
                MessageBox.Show("Тесту применения нужны права администратора.", "Klondaik Tweaker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var target = applyTest + 1 < args.Length && !args[applyTest + 1].StartsWith("--")
                ? args[applyTest + 1]
                : Path.Combine(Paths.Root, "selftest-apply.txt");
            Host.SelfTest.RunApply(target, canaryOnly, everything);
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        if (!IsAdmin() && Environment.GetEnvironmentVariable("KLONDAIK_UI_PREVIEW") != "1")
        {
            MessageBox.Show("Программе нужны права администратора. Запустите её от имени администратора.",
                "Klondaik Tweaker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!RuntimeAvailable())
        {
            var r = MessageBox.Show(
                "Не найден WebView2 Runtime — без него интерфейс не запустится.\n\nОткрыть страницу загрузки?",
                "Klondaik Tweaker", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r == DialogResult.Yes)
                Process.Start(new ProcessStartInfo("https://developer.microsoft.com/microsoft-edge/webview2/") { UseShellExecute = true });
            return;
        }

        Application.Run(new MainForm());
    }

    private static bool IsAdmin()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    private static bool RuntimeAvailable()
    {
        try { return !string.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); }
        catch { return false; }
    }

    private static void Crash(Exception? ex)
    {
        Log(ex);
        MessageBox.Show("Произошла ошибка:\n\n" + (ex?.Message ?? "неизвестно") + "\n\nПодробности в " + Paths.Logs,
            "Klondaik Tweaker", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    internal static void Log(Exception? ex)
    {
        try
        {
            var file = Path.Combine(Paths.Logs, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            File.AppendAllText(file, $"[{DateTime.Now:HH:mm:ss}] {ex}\n\n");
        }
        catch { }
    }

    internal static void Log(string message)
    {
        try
        {
            var file = Path.Combine(Paths.Logs, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
            File.AppendAllText(file, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
        catch { }
    }
}
