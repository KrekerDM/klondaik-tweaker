using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;

namespace KlondaikTweaker.Core.Win;

public static class Ti
{
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint TOKEN_DUPLICATE = 0x0002;
    private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
    private const uint TOKEN_QUERY = 0x0008;
    private const uint MAXIMUM_ALLOWED = 0x02000000;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint LOGON_WITH_PROFILE = 0x00000001;
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public uint ProcessId;
        public uint ThreadId;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
        public short ShowWindow, Reserved2;
        public IntPtr Reserved3;
        public IntPtr StdInput, StdOutput, StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ServiceStatusProcess
    {
        public int ServiceType;
        public int CurrentState;
        public int ControlsAccepted;
        public int Win32ExitCode;
        public int ServiceSpecificExitCode;
        public int CheckPoint;
        public int WaitHint;
        public int ProcessId;
        public int ServiceFlags;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(IntPtr existing, uint access, IntPtr attributes, int level, int type, out IntPtr duplicate);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessWithTokenW(IntPtr token, uint logonFlags, string? applicationName,
        string commandLine, uint creationFlags, IntPtr environment, string? currentDirectory,
        ref StartupInfo startupInfo, out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenSCManagerW(string? machine, string? database, uint access);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr OpenServiceW(IntPtr manager, string name, uint access);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool QueryServiceStatusEx(IntPtr service, int infoLevel, IntPtr buffer, int size, out int needed);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool CloseServiceHandle(IntPtr handle);

    public static string? LastError { get; private set; }

    private static int TrustedInstallerPid()
    {
        try
        {
            using var sc = new ServiceController("TrustedInstaller");
            if (sc.Status != ServiceControllerStatus.Running)
            {
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(25));
            }
        }
        catch (Exception ex)
        {
            LastError = "cannot start TrustedInstaller: " + ex.Message;
            return 0;
        }

        var manager = OpenSCManagerW(null, null, 0x0001);
        if (manager == IntPtr.Zero) { LastError = "OpenSCManager failed"; return 0; }
        var service = IntPtr.Zero;
        var buffer = IntPtr.Zero;
        try
        {
            service = OpenServiceW(manager, "TrustedInstaller", 0x0004);
            if (service == IntPtr.Zero) { LastError = "OpenService failed"; return 0; }
            var size = Marshal.SizeOf<ServiceStatusProcess>();
            buffer = Marshal.AllocHGlobal(size);
            if (!QueryServiceStatusEx(service, 0, buffer, size, out _)) { LastError = "QueryServiceStatusEx failed"; return 0; }
            return Marshal.PtrToStructure<ServiceStatusProcess>(buffer).ProcessId;
        }
        finally
        {
            if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
            if (service != IntPtr.Zero) CloseServiceHandle(service);
            CloseServiceHandle(manager);
        }
    }

    private static int SystemPid()
    {
        foreach (var name in new[] { "winlogon", "services", "lsass" })
        {
            try
            {
                var found = System.Diagnostics.Process.GetProcessesByName(name);
                if (found.Length > 0)
                {
                    var pid = found[0].Id;
                    foreach (var p in found) p.Dispose();
                    return pid;
                }
            }
            catch { }
        }
        return 0;
    }

    public static string LastIdentity { get; private set; } = "";

    public static bool Available()
    {
        Elevate.EnableAll();
        return TrustedInstallerPid() != 0 || SystemPid() != 0;
    }

    public static ShResult Run(string commandLine, int timeoutMs = 180000)
    {
        Elevate.EnableAll();
        LastError = null;

        var pid = TrustedInstallerPid();
        LastIdentity = "TrustedInstaller";
        if (pid == 0)
        {
            pid = SystemPid();
            LastIdentity = "System";
        }
        if (pid == 0)
        {
            LastIdentity = "";
            return new ShResult(-10, "", LastError ?? "no elevated token available");
        }

        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (process == IntPtr.Zero) return new ShResult(-11, "", "OpenProcess failed: " + Marshal.GetLastWin32Error());

        var token = IntPtr.Zero;
        var duplicate = IntPtr.Zero;
        var output = Path.Combine(Path.GetTempPath(), "kl-ti-" + Guid.NewGuid().ToString("n") + ".txt");

        try
        {
            if (!OpenProcessToken(process, TOKEN_DUPLICATE | TOKEN_QUERY, out token))
                return new ShResult(-12, "", "OpenProcessToken failed: " + Marshal.GetLastWin32Error());

            if (!DuplicateTokenEx(token, MAXIMUM_ALLOWED, IntPtr.Zero, SecurityImpersonation, TokenPrimary, out duplicate))
                return new ShResult(-13, "", "DuplicateTokenEx failed: " + Marshal.GetLastWin32Error());

            var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>(), Desktop = "WinSta0\\Default" };
            var wrapped = $"cmd.exe /d /c chcp 65001 >nul & ({commandLine}) > \"{output}\" 2>&1";

            if (!CreateProcessWithTokenW(duplicate, 0, null, wrapped, CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT,
                    IntPtr.Zero, null, ref startup, out var info))
                return new ShResult(-14, "", "CreateProcessWithTokenW failed: " + Marshal.GetLastWin32Error());

            WaitForSingleObject(info.Process, (uint)timeoutMs);
            GetExitCodeProcess(info.Process, out var code);
            CloseHandle(info.Thread);
            CloseHandle(info.Process);

            var text = "";
            try
            {
                if (File.Exists(output)) text = File.ReadAllText(output, Encoding.UTF8).Trim();
            }
            catch { }

            return new ShResult((int)code, text, code == 0 ? "" : text);
        }
        catch (Exception ex)
        {
            return new ShResult(-15, "", ex.Message);
        }
        finally
        {
            if (duplicate != IntPtr.Zero) CloseHandle(duplicate);
            if (token != IntPtr.Zero) CloseHandle(token);
            CloseHandle(process);
            try { if (File.Exists(output)) File.Delete(output); } catch { }
        }
    }

    public static ShResult RunExe(string exe, string args, int timeoutMs = 180000)
        => Run($"\"{exe}\" {args}", timeoutMs);
}
