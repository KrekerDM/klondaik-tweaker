using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace KlondaikTweaker.Core.Win;

public sealed record ShResult(int Code, string Out, string Err)
{
    public bool Ok => Code == 0;
    public string All => (Out + "\n" + Err).Trim();
}

public static class Sh
{
    public static readonly Encoding Console = Resolve("OEMCP", 866);
    public static readonly Encoding Ansi = Resolve("ACP", 1251);

    private static Encoding Resolve(string valueName, int fallback)
    {
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); } catch { }

        var page = fallback;
        try
        {
            var raw = Reg.Read("HKLM", NlsPath, valueName).Value;
            if (int.TryParse(raw, out var parsed) && parsed > 0) page = parsed;
        }
        catch { }

        try { return Encoding.GetEncoding(page); }
        catch { return Encoding.UTF8; }
    }

    private const string NlsPath = "SYSTEM" + "\\" + "CurrentControlSet" + "\\" + "Control" + "\\" + "Nls" + "\\" + "CodePage";

    public static ShResult Run(string exe, string args, int timeoutMs = 60000, string? workDir = null, Encoding? encoding = null)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = encoding ?? Console,
                StandardErrorEncoding = encoding ?? Console
            };
            if (workDir is not null) psi.WorkingDirectory = workDir;
            using var p = Process.Start(psi);
            if (p is null) return new ShResult(-1, "", "start failed");
            var so = p.StandardOutput.ReadToEndAsync();
            var se = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(true); } catch { }
                return new ShResult(-2, "", "timeout");
            }
            return new ShResult(p.ExitCode, so.Result.Trim(), se.Result.Trim());
        }
        catch (Exception ex) { return new ShResult(-3, "", ex.Message); }
    }

    public static ShResult Ps(string script, int timeoutMs = 120000)
    {
        var enc = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        return Run("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand " + enc, timeoutMs);
    }

    public static void OpenExternal(string target)
    {
        try { Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true }); } catch { }
    }
}
