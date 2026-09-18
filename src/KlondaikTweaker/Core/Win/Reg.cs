using Microsoft.Win32;
using System.Globalization;

namespace KlondaikTweaker.Core.Win;

public static class Reg
{
    public static RegistryKey? Root(string hive) => hive.ToUpperInvariant() switch
    {
        "HKLM" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
        "HKCU" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64),
        "HKCR" => RegistryKey.OpenBaseKey(RegistryHive.ClassesRoot, RegistryView.Registry64),
        "HKU" => RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64),
        _ => null
    };

    public static string KindOf(RegistryValueKind k) => k switch
    {
        RegistryValueKind.DWord => "dword",
        RegistryValueKind.QWord => "qword",
        RegistryValueKind.ExpandString => "expand",
        RegistryValueKind.MultiString => "multi",
        RegistryValueKind.Binary => "binary",
        _ => "sz"
    };

    public static RegistryValueKind KindFrom(string? t) => (t ?? "sz").ToLowerInvariant() switch
    {
        "dword" => RegistryValueKind.DWord,
        "qword" => RegistryValueKind.QWord,
        "expand" => RegistryValueKind.ExpandString,
        "multi" => RegistryValueKind.MultiString,
        "binary" => RegistryValueKind.Binary,
        _ => RegistryValueKind.String
    };

    public static long ParseNum(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return long.Parse(s[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return long.Parse(s, CultureInfo.InvariantCulture);
    }

    public static byte[] ParseHex(string s)
    {
        var clean = new string(s.Where(Uri.IsHexDigit).ToArray());
        if (clean.Length % 2 != 0) clean = "0" + clean;
        var b = new byte[clean.Length / 2];
        for (int i = 0; i < b.Length; i++) b[i] = byte.Parse(clean.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return b;
    }

    public static object Materialize(string? type, string value)
    {
        return KindFrom(type) switch
        {
            RegistryValueKind.DWord => unchecked((int)ParseNum(value)),
            RegistryValueKind.QWord => ParseNum(value),
            RegistryValueKind.MultiString => value.Length == 0 ? Array.Empty<string>() : value.Split('|'),
            RegistryValueKind.Binary => ParseHex(value),
            _ => value
        };
    }

    public static string Stringify(object? v) => v switch
    {
        null => "",
        int i => i.ToString(CultureInfo.InvariantCulture),
        long l => l.ToString(CultureInfo.InvariantCulture),
        string[] a => string.Join("|", a),
        byte[] b => Convert.ToHexString(b),
        _ => v.ToString() ?? ""
    };

    public sealed record ReadResult(bool Exists, string Kind, string Value);

    public static ReadResult Read(string hive, string path, string? name)
    {
        try
        {
            using var root = Root(hive);
            if (root is null) return new(false, "sz", "");
            using var key = root.OpenSubKey(path, false);
            if (key is null) return new(false, "sz", "");
            if (string.IsNullOrEmpty(name)) name = "";
            var raw = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            if (raw is null) return new(false, "sz", "");
            return new(true, KindOf(key.GetValueKind(name)), Stringify(raw));
        }
        catch { return new(false, "sz", ""); }
    }

    public static void Write(string hive, string path, string? name, string? type, string value)
    {
        using var root = Root(hive) ?? throw new InvalidOperationException("hive " + hive);
        using var key = root.CreateSubKey(path, true) ?? throw new InvalidOperationException("key " + path);
        key.SetValue(name ?? "", Materialize(type, value), KindFrom(type));
    }

    public static void DeleteValue(string hive, string path, string? name)
    {
        using var root = Root(hive);
        using var key = root?.OpenSubKey(path, true);
        if (key is null) return;
        try { key.DeleteValue(name ?? "", false); } catch { }
    }

    public static void DeleteTree(string hive, string path)
    {
        using var root = Root(hive);
        if (root is null) return;
        try { root.DeleteSubKeyTree(path, false); } catch { }
    }

    public static bool KeyExists(string hive, string path)
    {
        using var root = Root(hive);
        using var key = root?.OpenSubKey(path, false);
        return key is not null;
    }

    public static bool Same(string? type, string expected, string current)
    {
        var kind = KindFrom(type);
        if (kind is RegistryValueKind.DWord or RegistryValueKind.QWord)
        {
            try { return ParseNum(expected) == ParseNum(current); } catch { return false; }
        }
        if (kind == RegistryValueKind.Binary)
            return string.Equals(current.Replace(" ", ""), Convert.ToHexString(ParseHex(expected)), StringComparison.OrdinalIgnoreCase);
        return string.Equals(expected, current, StringComparison.OrdinalIgnoreCase);
    }

    public static string[] SubKeys(string hive, string path)
    {
        try
        {
            using var root = Root(hive);
            using var key = root?.OpenSubKey(path, false);
            return key?.GetSubKeyNames() ?? [];
        }
        catch { return []; }
    }

    public static (string Name, string Value)[] Values(string hive, string path)
    {
        try
        {
            using var root = Root(hive);
            using var key = root?.OpenSubKey(path, false);
            if (key is null) return [];
            return key.GetValueNames().Select(n => (n, Stringify(key.GetValue(n)))).ToArray();
        }
        catch { return []; }
    }
}
