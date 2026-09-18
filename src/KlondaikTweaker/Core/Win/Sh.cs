using System.Diagnostics;
using System.Text;

namespace KlondaikTweaker.Core.Win;

public sealed record ShResult(int Code, string Out, string Err)
{
    public bool Ok => Code == 0;
    public string All => (Out + "\n" + Err).Trim();
}

public static class Sh
{
    public static ShResult Run(string exe, string args, int timeoutMs = 60000, string? workDir = null)
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
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
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
