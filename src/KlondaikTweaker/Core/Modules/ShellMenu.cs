using System.Diagnostics;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Model;
using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public static class ShellMenu
{
    private const string TiVerb = "KlondaikRunAsTI";
    private const string OwnVerb = "KlondaikTakeOwnership";
    private const string Classes = @"SOFTWARE\Classes";

    private static readonly string[] TiTargets = [@"*\shell", @"Directory\shell"];
    private static readonly string[] OwnTargets = [@"*\shell", @"Directory\shell", @"Drive\shell"];

    private static string Exe => Environment.ProcessPath ?? "";

    private static bool Ru => !Texts.English;

    public static bool TiInstalled => Reg.KeyExists("HKLM", $@"{Classes}\*\shell\{TiVerb}");

    public static bool OwnInstalled => Reg.KeyExists("HKLM", $@"{Classes}\*\shell\{OwnVerb}");

    public static RepairResult ToggleTi() => TiInstalled ? Remove(TiVerb, TiTargets, true) : InstallTi();

    public static RepairResult ToggleOwn() => OwnInstalled ? Remove(OwnVerb, OwnTargets, false) : InstallOwn();

    private static RepairResult InstallTi()
    {
        var exe = Exe;
        if (string.IsNullOrEmpty(exe))
            return new RepairResult { Ok = false, Message = Ru ? "не удалось определить путь к программе" : "cannot locate the program" };

        var label = Ru ? "Запустить от имени TrustedInstaller" : "Run as TrustedInstaller";
        return Install(TiVerb, TiTargets, label, exe, "--runas-ti", true);
    }

    private static RepairResult InstallOwn()
    {
        var exe = Exe;
        if (string.IsNullOrEmpty(exe))
            return new RepairResult { Ok = false, Message = Ru ? "не удалось определить путь к программе" : "cannot locate the program" };

        var label = Ru ? "Стать владельцем" : "Take ownership";
        return Install(OwnVerb, OwnTargets, label, exe, "--take-ownership", false);
    }

    private static RepairResult Install(string verb, string[] targets, string label, string exe, string flag, bool shield)
    {
        var result = new RepairResult();
        var entry = new JournalEntry { TweakId = "shell." + verb, Title = label, Group = "repair" };

        foreach (var target in targets)
        {
            var key = $@"{Classes}\{target}\{verb}";
            try
            {
                var existed = Reg.KeyExists("HKLM", key);
                if (!existed)
                    entry.Items.Add(new JournalItem { Kind = "regnewkey", Target = "HKLM\\" + key });

                Reg.Write("HKLM", key, "", "sz", label);
                Reg.Write("HKLM", key, "Icon", "sz", exe + ",0");
                if (shield) Reg.Write("HKLM", key, "HasLUAShield", "sz", "");
                Reg.Write("HKLM", key, "NoWorkingDirectory", "sz", "");
                Reg.Write("HKLM", key + @"\command", "", "sz", $"\"{exe}\" {flag} \"%1\"");
                result.Changed++;
            }
            catch (Exception ex)
            {
                result.Details.Add(target + ": " + Trim(ex.Message));
            }
        }

        if (result.Changed == 0)
        {
            result.Ok = false;
            result.Message = Ru ? "не удалось добавить пункты" : "could not add the entries";
            return result;
        }

        if (entry.Items.Count > 0) Journal.Add(entry);
        result.Message = Ru ? "пункт добавлен в контекстное меню" : "entry added to the context menu";
        return result;
    }

    private static RepairResult Remove(string verb, string[] targets, bool ti)
    {
        var result = new RepairResult();
        foreach (var target in targets)
        {
            var key = $@"{Classes}\{target}\{verb}";
            try
            {
                if (!Reg.KeyExists("HKLM", key)) continue;
                Reg.DeleteTree("HKLM", key);
                result.Changed++;
            }
            catch (Exception ex)
            {
                result.Details.Add(target + ": " + Trim(ex.Message));
            }
        }

        result.Message = result.Changed > 0
            ? (Ru ? "пункт убран из контекстного меню" : "entry removed from the context menu")
            : (Ru ? "пункта не было" : "the entry was not there");
        return result;
    }

    public static int RunAsTi(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) && !Directory.Exists(path))
        {
            Report(Ru ? "Путь не найден:\n" + path : "Path not found:\n" + path, true);
            return 2;
        }

        Elevate.EnableAll();

        if (Directory.Exists(path))
        {
            var r = Ti.Run($"cmd.exe /k cd /d \"{path}\"");
            if (!r.Ok) Report(Ru ? "Не удалось запустить консоль:\n" + Trim(r.All) : "Could not start the console:\n" + Trim(r.All), true);
            return r.Ok ? 0 : 1;
        }

        var run = Ti.RunExe(path, "");
        if (!run.Ok)
            Report(Ru
                ? "Не удалось запустить от имени TrustedInstaller:\n" + Trim(run.All)
                : "Could not run as TrustedInstaller:\n" + Trim(run.All), true);
        return run.Ok ? 0 : 1;
    }

    public static int TakeOwnership(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) && !Directory.Exists(path))
        {
            Report(Ru ? "Путь не найден:\n" + path : "Path not found:\n" + path, true);
            return 2;
        }

        Elevate.EnableAll();
        var dir = Directory.Exists(path);

        var own = Sh.Run("takeown.exe", dir ? $"/f \"{path}\" /r /d Y" : $"/f \"{path}\"", 600000);
        var acl = Sh.Run("icacls.exe", dir
            ? $"\"{path}\" /grant *S-1-5-32-544:F /t /c /q"
            : $"\"{path}\" /grant *S-1-5-32-544:F /c /q", 600000);

        if (!own.Ok && !acl.Ok)
        {
            var ti = Ti.Run($"cmd.exe /c takeown /f \"{path}\"{(dir ? " /r /d Y" : "")} && icacls \"{path}\" /grant *S-1-5-32-544:F{(dir ? " /t" : "")} /c /q");
            if (!ti.Ok)
            {
                Report(Ru ? "Не удалось стать владельцем:\n" + Trim(ti.All) : "Could not take ownership:\n" + Trim(ti.All), true);
                return 1;
            }
        }

        Report(Ru ? "Владелец изменён, полный доступ выдан администраторам." : "Ownership changed, Administrators granted full control.", false);
        return 0;
    }

    private static void Report(string text, bool error)
    {
        try
        {
            MessageBox.Show(text, "Klondaik Tweaker", MessageBoxButtons.OK,
                error ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch
        {
            Debug.WriteLine(text);
        }
    }

    private static string Trim(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 300 ? s[..300] : s;
    }
}
