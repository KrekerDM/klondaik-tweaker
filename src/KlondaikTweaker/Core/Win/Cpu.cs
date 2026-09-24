using System.Runtime.InteropServices;

namespace KlondaikTweaker.Core.Win;

public sealed class CpuThread
{
    public int Index { get; set; }
    public int Core { get; set; }
    public int Efficiency { get; set; }
    public bool Performance { get; set; }
}

public static class Cpu
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetLogicalProcessorInformationEx(int relationship, IntPtr buffer, ref int returnedLength);

    private const int RelationProcessorCore = 0;

    private static List<CpuThread>? _cache;
    private static readonly object Gate = new();

    public static List<CpuThread> Threads()
    {
        lock (Gate)
        {
            if (_cache is not null) return _cache;
            _cache = Read();
            return _cache;
        }
    }

    public static bool Hybrid() => Threads().Select(x => x.Efficiency).Distinct().Count() > 1;

    public static int Count() => Threads().Count;

    private static List<CpuThread> Read()
    {
        var list = new List<CpuThread>();
        var length = 0;
        GetLogicalProcessorInformationEx(RelationProcessorCore, IntPtr.Zero, ref length);
        if (length <= 0) return Fallback();

        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            if (!GetLogicalProcessorInformationEx(RelationProcessorCore, buffer, ref length)) return Fallback();

            var offset = 0;
            var core = 0;
            while (offset < length)
            {
                var entry = buffer + offset;
                var size = Marshal.ReadInt32(entry, 4);
                if (size <= 0) break;

                var efficiency = Marshal.ReadByte(entry, 9);
                var groupCount = Marshal.ReadInt16(entry, 30);
                if (groupCount < 1) groupCount = 1;

                for (var g = 0; g < groupCount; g++)
                {
                    var groupOffset = 32 + g * 16;
                    var mask = (ulong)Marshal.ReadInt64(entry, groupOffset);
                    var group = Marshal.ReadInt16(entry, groupOffset + 8);
                    if (group != 0) continue;

                    for (var bit = 0; bit < 64; bit++)
                    {
                        if ((mask & (1UL << bit)) == 0) continue;
                        list.Add(new CpuThread { Index = bit, Core = core, Efficiency = efficiency });
                    }
                }

                core++;
                offset += size;
            }
        }
        catch { return Fallback(); }
        finally { Marshal.FreeHGlobal(buffer); }

        if (list.Count == 0) return Fallback();

        var best = list.Max(x => x.Efficiency);
        var worst = list.Min(x => x.Efficiency);
        foreach (var t in list) t.Performance = best == worst || t.Efficiency == best;

        return list.OrderBy(x => x.Index).ToList();
    }

    private static List<CpuThread> Fallback()
    {
        var count = Math.Max(1, Environment.ProcessorCount);
        return Enumerable.Range(0, Math.Min(64, count))
            .Select(i => new CpuThread { Index = i, Core = i / 2, Efficiency = 0, Performance = true })
            .ToList();
    }

    public static void Invalidate()
    {
        lock (Gate) _cache = null;
    }
}
