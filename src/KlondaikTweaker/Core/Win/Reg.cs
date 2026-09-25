using System.IO;
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

    public static int Escalations { get; private set; }

    private static void WriteDirect(string hive, string path, string? name, string? type, string value)
    {
        using var root = Root(hive) ?? throw new InvalidOperationException("hive " + hive);
        using var key = root.CreateSubKey(path, true) ?? throw new InvalidOperationException("key " + path);
        key.SetValue(name ?? "", Materialize(type, value), KindFrom(type));
    }

    private static bool IsPendingDeletion(IOException e) => (uint)e.HResult == 0x800703FA;

    public static void Write(string hive, string path, string? name, string? type, string value)
    {
        try
        {
            WriteDirect(hive, path, name, type, value);
            return;
        }
        catch (IOException pending) when (IsPendingDeletion(pending))
        {
            Thread.Sleep(200);
            WriteDirect(hive, path, name, type, value);
            return;
        }
        catch (Exception first) when (first is UnauthorizedAccessException or System.Security.SecurityException)
        {
            if (Elevate.UnlockKey(hive, path))
            {
                try
                {
                    WriteDirect(hive, path, name, type, value);
                    Escalations++;
                    return;
                }
                catch { }
            }

            var r = Ti.Run(RegAdd(hive, path, name, type, value));
            if (!r.Ok) throw new UnauthorizedAccessException($"{hive}\\{path}\\{name}: {Brief(r.All, first.Message)}");
            Escalations++;
        }
    }

    public static void DeleteValue(string hive, string path, string? name)
    {
        try
        {
            using var root = Root(hive);
            using var key = root?.OpenSubKey(path, true);
            if (key is null) return;
            key.DeleteValue(name ?? "", false);
            return;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            if (Elevate.UnlockKey(hive, path))
            {
                try
                {
                    using var root = Root(hive);
                    using var key = root?.OpenSubKey(path, true);
                    key?.DeleteValue(name ?? "", false);
                    Escalations++;
                    return;
                }
                catch { }
            }
            Ti.Run($"reg delete \"{FullPath(hive, path)}\" /v \"{name}\" /f");
            Escalations++;
        }
        catch { }
    }

    public static void DeleteTree(string hive, string path)
    {
        try
        {
            using var root = Root(hive);
            if (root is null) return;
            root.DeleteSubKeyTree(path, false);
            return;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
        {
            if (Elevate.UnlockTree(hive, path))
            {
                try
                {
                    using var root = Root(hive);
                    root?.DeleteSubKeyTree(path, false);
                    Escalations++;
                    return;
                }
                catch { }
            }
            Ti.Run($"reg delete \"{FullPath(hive, path)}\" /f");
            Escalations++;
        }
        catch { }
    }

    private static string FullPath(string hive, string path) => hive.ToUpperInvariant() switch
    {
        "HKLM" => "HKLM\\" + path,
        "HKCU" => "HKCU\\" + path,
        "HKCR" => "HKCR\\" + path,
        "HKU" => "HKU\\" + path,
        _ => hive + "\\" + path
    };

    private static string RegType(string? type) => (type ?? "sz").ToLowerInvariant() switch
    {
        "dword" => "REG_DWORD",
        "qword" => "REG_QWORD",
        "expand" => "REG_EXPAND_SZ",
        "multi" => "REG_MULTI_SZ",
        "binary" => "REG_BINARY",
        _ => "REG_SZ"
    };

    private static string RegAdd(string hive, string path, string? name, string? type, string value)
    {
        var data = value.Replace("\"", "\\\"");
        if (KindFrom(type) == RegistryValueKind.MultiString) data = data.Replace("|", "\\0");
        var nameArg = string.IsNullOrEmpty(name) ? "/ve" : $"/v \"{name}\"";
        return $"reg add \"{FullPath(hive, path)}\" {nameArg} /t {RegType(type)} /d \"{data}\" /f";
    }

    private static string Brief(string a, string b)
    {
        var text = string.IsNullOrWhiteSpace(a) ? b : a;
        text = text.Replace("\r", " ").Replace("\n", " ").Trim();
        return text.Length > 140 ? text[..140] : text;
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
        if (kind == RegistryValueKind.DWord)
        {
            try { return unchecked((uint)ParseNum(expected)) == unchecked((uint)ParseNum(current)); }
            catch { return false; }
        }
        if (kind == RegistryValueKind.QWord)
        {
            try { return unchecked((ulong)ParseNum(expected)) == unchecked((ulong)ParseNum(current)); }
            catch { return false; }
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
