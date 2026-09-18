using System.Runtime.InteropServices;

namespace KlondaikTweaker.Core.Win;

public sealed class PdhCounter : IDisposable
{
    private const uint FmtDouble = 0x00000200;
    private const uint FmtNoCap100 = 0x00008000;
    private const uint MoreData = 0x800007D2;

    [StructLayout(LayoutKind.Explicit)]
    private struct CounterValue
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double DoubleValue;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CounterItem
    {
        public IntPtr Name;
        public CounterValue Value;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string path, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, out uint itemCount, IntPtr buffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

    private IntPtr _query;
    private readonly Dictionary<string, IntPtr> _counters = new();
    private bool _primed;

    public bool Ok => _query != IntPtr.Zero;

    public PdhCounter()
    {
        if (PdhOpenQueryW(null, IntPtr.Zero, out _query) != 0) _query = IntPtr.Zero;
    }

    public bool Add(string key, string path)
    {
        if (_query == IntPtr.Zero) return false;
        if (_counters.ContainsKey(key)) return true;
        if (PdhAddEnglishCounterW(_query, path, IntPtr.Zero, out var c) != 0) return false;
        _counters[key] = c;
        return true;
    }

    public void Collect()
    {
        if (_query == IntPtr.Zero) return;
        PdhCollectQueryData(_query);
        _primed = true;
    }

    public double Sum(string key)
    {
        if (!_primed || !_counters.TryGetValue(key, out var c)) return 0;
        uint size = 0;
        var status = PdhGetFormattedCounterArrayW(c, FmtDouble | FmtNoCap100, ref size, out _, IntPtr.Zero);
        if (status != MoreData || size == 0) return 0;
        var buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArrayW(c, FmtDouble | FmtNoCap100, ref size, out var count, buf) != 0) return 0;
            double total = 0;
            var stride = Marshal.SizeOf<CounterItem>();
            for (int i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<CounterItem>(buf + i * stride);
                if (item.Value.CStatus == 0 && !double.IsNaN(item.Value.DoubleValue)) total += item.Value.DoubleValue;
            }
            return total;
        }
        catch { return 0; }
        finally { Marshal.FreeHGlobal(buf); }
    }

    public double Max(string key)
    {
        if (!_primed || !_counters.TryGetValue(key, out var c)) return 0;
        uint size = 0;
        var status = PdhGetFormattedCounterArrayW(c, FmtDouble | FmtNoCap100, ref size, out _, IntPtr.Zero);
        if (status != MoreData || size == 0) return 0;
        var buf = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArrayW(c, FmtDouble | FmtNoCap100, ref size, out var count, buf) != 0) return 0;
            double max = 0;
            var stride = Marshal.SizeOf<CounterItem>();
            for (int i = 0; i < count; i++)
            {
                var item = Marshal.PtrToStructure<CounterItem>(buf + i * stride);
                if (item.Value.CStatus == 0 && item.Value.DoubleValue > max) max = item.Value.DoubleValue;
            }
            return max;
        }
        catch { return 0; }
        finally { Marshal.FreeHGlobal(buf); }
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            PdhCloseQuery(_query);
            _query = IntPtr.Zero;
        }
        GC.SuppressFinalize(this);
    }
}
