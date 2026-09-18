using System.Runtime.InteropServices;

namespace KlondaikTweaker.Core.Win;

public static class Sys
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus status);

    public static bool HasBattery()
    {
        try
        {
            if (!GetSystemPowerStatus(out var s)) return false;
            return s.BatteryFlag != 128 && s.BatteryFlag != 255;
        }
        catch { return false; }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLogicalProcessorInformationEx(int relationship, IntPtr buffer, ref int length);

    public static int PhysicalCores()
    {
        const int RelationProcessorCore = 0;
        int length = 0;
        try
        {
            GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref length);
            if (length == 0) return 0;
            var buffer = Marshal.AllocHGlobal(length);
            try
            {
                if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length)) return 0;
                int offset = 0;
                int count = 0;
                while (offset < length)
                {
                    var size = Marshal.ReadInt32(buffer + offset + 4);
                    if (size <= 0) break;
                    count++;
                    offset += size;
                }
                return count;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        catch { return 0; }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int cb;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
        public int StateFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool EnumDisplayDevicesW(string? device, uint index, ref DisplayDevice info, uint flags);

    public static string[] Adapters()
    {
        var names = new List<string>();
        try
        {
            for (uint i = 0; i < 16; i++)
            {
                var info = new DisplayDevice();
                info.cb = Marshal.SizeOf<DisplayDevice>();
                if (!EnumDisplayDevicesW(null, i, ref info, 0)) break;
                var name = (info.DeviceString ?? "").Trim();
                if (name.Length > 0 && !names.Contains(name, StringComparer.OrdinalIgnoreCase)) names.Add(name);
            }
        }
        catch { }

        if (names.Count == 0)
        {
            const string classKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
            foreach (var sub in Reg.SubKeys("HKLM", classKey))
            {
                if (!int.TryParse(sub, out _)) continue;
                var desc = Reg.Read("HKLM", classKey + "\\" + sub, "DriverDesc").Value;
                if (desc.Length > 0 && !names.Contains(desc, StringComparer.OrdinalIgnoreCase)) names.Add(desc);
            }
        }
        return names.ToArray();
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(IntPtr device, uint code, IntPtr inBuffer, int inSize, IntPtr outBuffer, int outSize, out int returned, IntPtr overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct StoragePropertyQuery
    {
        public int PropertyId;
        public int QueryType;
        public byte AdditionalParameters;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SeekPenaltyDescriptor
    {
        public uint Version;
        public uint Size;
        [MarshalAs(UnmanagedType.U1)] public bool IncursSeekPenalty;
    }

    public static bool? IsSolidState(char driveLetter)
    {
        const uint IoctlStorageQueryProperty = 0x002D1400;
        const uint OpenExisting = 3;
        var handle = CreateFileW($@"\\.\{driveLetter}:", 0, 3, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return null;
        var inPtr = IntPtr.Zero;
        var outPtr = IntPtr.Zero;
        try
        {
            var query = new StoragePropertyQuery { PropertyId = 7, QueryType = 0 };
            var inSize = Marshal.SizeOf<StoragePropertyQuery>();
            var outSize = Marshal.SizeOf<SeekPenaltyDescriptor>();
            inPtr = Marshal.AllocHGlobal(inSize);
            outPtr = Marshal.AllocHGlobal(outSize);
            Marshal.StructureToPtr(query, inPtr, false);
            if (!DeviceIoControl(handle, IoctlStorageQueryProperty, inPtr, inSize, outPtr, outSize, out _, IntPtr.Zero))
                return null;
            var result = Marshal.PtrToStructure<SeekPenaltyDescriptor>(outPtr);
            return !result.IncursSeekPenalty;
        }
        catch { return null; }
        finally
        {
            if (inPtr != IntPtr.Zero) Marshal.FreeHGlobal(inPtr);
            if (outPtr != IntPtr.Zero) Marshal.FreeHGlobal(outPtr);
            CloseHandle(handle);
        }
    }
}
