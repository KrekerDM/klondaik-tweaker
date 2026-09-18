using KlondaikTweaker.Core.Win;

namespace KlondaikTweaker.Core.Modules;

public sealed class CleanTarget
{
    public string Id { get; set; } = "";
    public string Ru { get; set; } = "";
    public string En { get; set; } = "";
    public string DescRu { get; set; } = "";
    public string DescEn { get; set; } = "";
    public long Bytes { get; set; }
    public int Files { get; set; }
    public bool Safe { get; set; } = true;
    public bool Default { get; set; } = true;
}

public static class Cleaner
{
    private static string Local => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static string Roaming => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static string WinDir => Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    private static List<string> Dirs(string id) => id switch
    {
        "temp.user" => [Path.GetTempPath()],
        "temp.win" => [Path.Combine(WinDir, "Temp")],
        "prefetch" => [Path.Combine(WinDir, "Prefetch")],
        "wu.cache" => [Path.Combine(WinDir, "SoftwareDistribution", "Download")],
        "do.cache" => [Path.Combine(WinDir, "SoftwareDistribution", "DeliveryOptimization")],
        "crash" => [Path.Combine(Local, "CrashDumps"), Path.Combine(WinDir, "Minidump"), Path.Combine(Roaming, "Microsoft", "Windows", "WER"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows", "WER")],
        "logs" => [Path.Combine(WinDir, "Logs", "CBS"), Path.Combine(WinDir, "Logs", "DISM"), Path.Combine(WinDir, "Logs", "WindowsUpdate"), Path.Combine(WinDir, "Panther")],
        "shader" => [Path.Combine(Local, "D3DSCache"), Path.Combine(Local, "NVIDIA", "DXCache"), Path.Combine(Local, "NVIDIA", "GLCache"), Path.Combine(Local, "AMD", "DxCache"), Path.Combine(Local, "AMD", "GLCache"), Path.Combine(Local, "Intel", "ShaderCache")],
        "thumbs" => [Path.Combine(Local, "Microsoft", "Windows", "Explorer")],
        "browsers" => BrowserDirs(),
        "installers" => [Path.Combine(Local, "Package Cache"), Path.Combine(Local, "Downloaded Installations")],
        "winold" => [Path.Combine(Path.GetPathRoot(WinDir) ?? "C:\\", "Windows.old")],
        "fontcache" => [Path.Combine(Local, "FontCache"), Path.Combine(WinDir, "ServiceProfiles", "LocalService", "AppData", "Local", "FontCache")],
        _ => []
    };

    private static List<string> BrowserDirs()
    {
        var list = new List<string>();
        void Chromium(string root, params string[] tail)
        {
            var b = Path.Combine(root, Path.Combine(tail));
            if (!Directory.Exists(b)) return;
            foreach (var profile in Directory.EnumerateDirectories(b))
            {
                foreach (var c in new[] { "Cache", "Code Cache", "GPUCache", "Service Worker\\CacheStorage", "DawnGraphiteCache", "DawnWebGPUCache" })
                {
                    var p = Path.Combine(profile, c);
                    if (Directory.Exists(p)) list.Add(p);
                }
            }
        }
        Chromium(Local, "Google", "Chrome", "User Data");
        Chromium(Local, "Microsoft", "Edge", "User Data");
        Chromium(Local, "BraveSoftware", "Brave-Browser", "User Data");
        Chromium(Local, "Yandex", "YandexBrowser", "User Data");
        Chromium(Roaming, "Opera Software", "Opera Stable");
        var ff = Path.Combine(Local, "Mozilla", "Firefox", "Profiles");
        if (Directory.Exists(ff))
            foreach (var p in Directory.EnumerateDirectories(ff))
            {
                var c = Path.Combine(p, "cache2");
                if (Directory.Exists(c)) list.Add(c);
            }
        return list;
    }

    private static readonly (string Id, string Ru, string En, string DRu, string DEn, bool Safe, bool Def)[] Defs =
    [
        ("temp.user", "Временные файлы пользователя", "User temp files", "Папка %TEMP% текущего пользователя", "Current user %TEMP% folder", true, true),
        ("temp.win", "Временные файлы Windows", "Windows temp files", "C:\\Windows\\Temp", "C:\\Windows\\Temp", true, true),
        ("wu.cache", "Кэш обновлений Windows", "Windows Update cache", "Скачанные пакеты обновлений, которые уже установлены", "Downloaded update packages already installed", true, true),
        ("do.cache", "Кэш Delivery Optimization", "Delivery Optimization cache", "Файлы раздачи обновлений другим ПК", "Files shared with other PCs for updates", true, true),
        ("crash", "Дампы и отчёты об ошибках", "Crash dumps and error reports", "Минидампы синих экранов и очередь Windows Error Reporting", "BSOD minidumps and Windows Error Reporting queue", true, true),
        ("logs", "Системные логи", "System logs", "Логи CBS, DISM, Windows Update, Panther", "CBS, DISM, Windows Update and Panther logs", true, true),
        ("shader", "Кэш шейдеров", "Shader cache", "DirectX, NVIDIA, AMD и Intel кэш шейдеров. Пересоберётся при запуске игр", "DirectX, NVIDIA, AMD and Intel shader caches. Rebuilt on next game launch", true, true),
        ("thumbs", "Кэш эскизов", "Thumbnail cache", "База миниатюр проводника, пересоздаётся автоматически", "Explorer thumbnail database, rebuilt automatically", true, true),
        ("browsers", "Кэш браузеров", "Browser caches", "Chrome, Edge, Brave, Opera, Yandex, Firefox. Пароли и вкладки не трогаются", "Chrome, Edge, Brave, Opera, Yandex, Firefox. Passwords and tabs untouched", true, true),
        ("prefetch", "Prefetch", "Prefetch", "Данные ускорения запуска программ. На SSD влияние минимально, пересоздаётся", "App launch prefetch data. Minimal effect on SSD, rebuilt automatically", false, false),
        ("installers", "Кэш установщиков", "Installer cache", "Package Cache: нужен для восстановления и удаления некоторых программ", "Package Cache: needed to repair or uninstall some apps", false, false),
        ("fontcache", "Кэш шрифтов", "Font cache", "Пересоздаётся при следующем входе в систему", "Rebuilt at next sign-in", false, false),
        ("winold", "Windows.old", "Windows.old", "Предыдущая установка Windows. После удаления откат на старую версию невозможен", "Previous Windows installation. Removing it makes rollback impossible", false, false),
        ("recycle", "Корзина", "Recycle Bin", "Полная очистка корзины на всех дисках", "Empty the Recycle Bin on all drives", true, false)
    ];

    public static List<CleanTarget> Scan()
    {
        var res = new List<CleanTarget>();
        foreach (var d in Defs)
        {
            var t = new CleanTarget
            {
                Id = d.Id,
                Ru = d.Ru,
                En = d.En,
                DescRu = d.DRu,
                DescEn = d.DEn,
                Safe = d.Safe,
                Default = d.Def
            };
            if (d.Id == "recycle") (t.Bytes, t.Files) = RecycleSize();
            else
            {
                foreach (var dir in Dirs(d.Id))
                {
                    var (b, f) = Measure(dir, d.Id == "thumbs" ? "thumbcache_*.db" : null);
                    t.Bytes += b;
                    t.Files += f;
                }
            }
            res.Add(t);
        }
        return res;
    }

    private static (long, int) Measure(string dir, string? pattern)
    {
        long bytes = 0;
        int count = 0;
        try
        {
            if (!Directory.Exists(dir)) return (0, 0);
            var opts = new EnumerationOptions { RecurseSubdirectories = pattern is null, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var f in Directory.EnumerateFiles(dir, pattern ?? "*", opts))
            {
                try
                {
                    var fi = new FileInfo(f);
                    bytes += fi.Length;
                    count++;
                    if (count > 400000) break;
                }
                catch { }
            }
        }
        catch { }
        return (bytes, count);
    }

    private static (long, int) RecycleSize()
    {
        long bytes = 0;
        int count = 0;
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                var bin = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                if (!Directory.Exists(bin)) continue;
                var (b, c) = Measure(bin, null);
                bytes += b;
                count += c;
            }
            catch { }
        }
        return (bytes, count);
    }

    public static (long Freed, int Files, List<string> Errors) Clean(IEnumerable<string> ids)
    {
        long freed = 0;
        int files = 0;
        var errors = new List<string>();
        var set = ids.ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (set.Contains("wu.cache") || set.Contains("do.cache"))
        {
            Svc.Stop("wuauserv");
            Svc.Stop("DoSvc");
        }

        foreach (var id in set)
        {
            if (id == "recycle")
            {
                var before = RecycleSize();
                Sh.Ps("Clear-RecycleBin -Force -ErrorAction SilentlyContinue", 120000);
                var after = RecycleSize();
                freed += Math.Max(0, before.Item1 - after.Item1);
                files += Math.Max(0, before.Item2 - after.Item2);
                continue;
            }
            if (id == "winold")
            {
                var dir = Dirs(id).FirstOrDefault();
                if (dir is not null && Directory.Exists(dir))
                {
                    var before = Measure(dir, null);
                    Sh.Run("cmd.exe", $"/c takeown /F \"{dir}\" /R /A /D Y >nul 2>&1 & icacls \"{dir}\" /grant administrators:F /T /C >nul 2>&1 & rd /s /q \"{dir}\"", 600000);
                    var after = Measure(dir, null);
                    freed += Math.Max(0, before.Item1 - after.Item1);
                    files += Math.Max(0, before.Item2 - after.Item2);
                }
                continue;
            }

            foreach (var dir in Dirs(id))
            {
                var (b, f, err) = Wipe(dir, id == "thumbs" ? "thumbcache_*.db" : null, keepRoot: true);
                freed += b;
                files += f;
                if (err is not null) errors.Add(err);
            }
        }

        if (set.Contains("wu.cache") || set.Contains("do.cache")) Svc.Start("wuauserv");
        return (freed, files, errors.Distinct().Take(5).ToList());
    }

    private static (long, int, string?) Wipe(string dir, string? pattern, bool keepRoot)
    {
        long freed = 0;
        int count = 0;
        string? error = null;
        try
        {
            if (!Directory.Exists(dir)) return (0, 0, null);
            var opts = new EnumerationOptions { RecurseSubdirectories = pattern is null, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
            foreach (var f in Directory.EnumerateFiles(dir, pattern ?? "*", opts).ToList())
            {
                try
                {
                    var fi = new FileInfo(f);
                    var len = fi.Length;
                    if (fi.IsReadOnly) fi.IsReadOnly = false;
                    fi.Delete();
                    freed += len;
                    count++;
                }
                catch { }
            }
            if (pattern is null)
            {
                foreach (var sub in Directory.EnumerateDirectories(dir).ToList())
                {
                    try { Directory.Delete(sub, true); } catch { }
                }
                if (!keepRoot) try { Directory.Delete(dir, true); } catch { }
            }
        }
        catch (Exception ex) { error = ex.Message; }
        return (freed, count, error);
    }

    public static string DeepComponentCleanup()
    {
        var r = Sh.Run("dism.exe", "/Online /Cleanup-Image /StartComponentCleanup /ResetBase", 1800000);
        return r.Ok ? "ok" : r.All;
    }

    public static string ClearEventLogs()
    {
        var r = Sh.Ps("wevtutil el | ForEach-Object { wevtutil cl \"$_\" 2>$null }", 300000);
        return r.Ok ? "ok" : r.All;
    }

    public static Dictionary<string, object> DiskInfo()
    {
        var drives = new List<object>();
        foreach (var d in DriveInfo.GetDrives())
        {
            try
            {
                if (!d.IsReady || d.DriveType != DriveType.Fixed) continue;
                drives.Add(new
                {
                    name = d.Name,
                    label = d.VolumeLabel,
                    total = d.TotalSize,
                    free = d.AvailableFreeSpace,
                    format = d.DriveFormat
                });
            }
            catch { }
        }
        return new Dictionary<string, object> { ["drives"] = drives };
    }
}
