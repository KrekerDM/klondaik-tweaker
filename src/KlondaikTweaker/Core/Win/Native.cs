using System.Runtime.InteropServices;

namespace KlondaikTweaker.Core.Win;

public static class Native
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public static MEMORYSTATUSEX Memory()
    {
        var m = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref m);
        return m;
    }

    [DllImport("kernel32.dll")]
    public static extern ulong GetTickCount64();

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hProcess);

    public static void TrimProcess(IntPtr handle) => EmptyWorkingSet(handle);

    [DllImport("ntdll.dll")]
    private static extern int NtSetSystemInformation(int infoClass, IntPtr info, int length);

    public static bool PurgeStandbyList()
    {
        try
        {
            int cmd = 4;
            var p = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(p, cmd);
            var r = NtSetSystemInformation(80, p, sizeof(int));
            Marshal.FreeHGlobal(p);
            return r == 0;
        }
        catch { return false; }
    }

    public static bool FlushModifiedList()
    {
        try
        {
            int cmd = 3;
            var p = Marshal.AllocHGlobal(sizeof(int));
            Marshal.WriteInt32(p, cmd);
            var r = NtSetSystemInformation(80, p, sizeof(int));
            Marshal.FreeHGlobal(p);
            return r == 0;
        }
        catch { return false; }
    }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr h, uint access, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool LookupPrivilegeValue(string? host, string name, out long luid);

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES { public int Count; public long Luid; public int Attr; }

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES newState, int len, IntPtr prev, IntPtr retLen);

    public static bool EnablePrivilege(string name)
    {
        try
        {
            if (!OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle, 0x0020 | 0x0008, out var token)) return false;
            if (!LookupPrivilegeValue(null, name, out var luid)) return false;
            var tp = new TOKEN_PRIVILEGES { Count = 1, Luid = luid, Attr = 2 };
            return AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        catch { return false; }
    }
}
