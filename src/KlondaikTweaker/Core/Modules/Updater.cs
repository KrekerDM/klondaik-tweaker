using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using KlondaikTweaker.Core.Engine;

namespace KlondaikTweaker.Core.Modules;

public sealed class UpdateInfo
{
    public bool Available { get; set; }
    public string Current { get; set; } = "";
    public string Latest { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Url { get; set; } = "";
    public string Page { get; set; } = "";
    public long Size { get; set; }
    public string? Error { get; set; }
}

public static class Updater
{
    public const string Owner = "KrekerDM";
    public const string Repo = "klondaik-tweaker";

    private const string AssetName = "KlondaikTweaker.exe";

    public static string CurrentVersion =>
        typeof(Updater).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public static string ReleasesPage => $"https://github.com/{Owner}/{Repo}/releases";

    private static HttpClient NewClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("KlondaikTweaker", CurrentVersion));
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return client;
    }

    private static Version Parse(string raw)
    {
        var clean = new string(raw.Where(c => char.IsDigit(c) || c == '.').ToArray()).Trim('.');
        var parts = clean.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var numbers = parts.Take(4).Select(x => int.TryParse(x, out var n) ? n : 0).ToArray();
        return numbers.Length switch
        {
            0 => new Version(0, 0),
            1 => new Version(numbers[0], 0),
            2 => new Version(numbers[0], numbers[1]),
            3 => new Version(numbers[0], numbers[1], numbers[2]),
            _ => new Version(numbers[0], numbers[1], numbers[2], numbers[3])
        };
    }

    public static UpdateInfo Check()
    {
        var info = new UpdateInfo { Current = CurrentVersion, Page = ReleasesPage };
        try
        {
            using var client = NewClient();
            client.Timeout = TimeSpan.FromSeconds(25);
            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            var body = client.GetStringAsync(url).GetAwaiter().GetResult();

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            info.Latest = root.TryGetProperty("tag_name", out var tag) ? tag.GetString() ?? "" : "";
            info.Notes = root.TryGetProperty("body", out var notes) ? (notes.GetString() ?? "").Trim() : "";
            if (info.Notes.Length > 1200) info.Notes = info.Notes[..1200] + "...";

            if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    if (!name.Equals(AssetName, StringComparison.OrdinalIgnoreCase)) continue;
                    info.Url = asset.TryGetProperty("browser_download_url", out var d) ? d.GetString() ?? "" : "";
                    info.Size = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                    break;
                }
            }

            info.Available = info.Latest.Length > 0
                             && Parse(info.Latest) > Parse(info.Current)
                             && info.Url.Length > 0;
        }
        catch (Exception ex)
        {
            info.Error = Short(ex.Message);
        }
        return info;
    }

    public static string Download(string url, Action<int>? progress = null)
    {
        var target = Path.Combine(Paths.Root, "update", AssetName);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        using var client = NewClient();
        using var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? 0;
        using var source = response.Content.ReadAsStream();
        using var file = File.Create(target);

        var buffer = new byte[128 * 1024];
        long done = 0;
        int last = -1;
        int read;
        while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            file.Write(buffer, 0, read);
            done += read;
            if (total <= 0) continue;
            var percent = (int)(done * 100 / total);
            if (percent != last)
            {
                last = percent;
                progress?.Invoke(percent);
            }
        }

        file.Flush(true);
        if (total > 0 && done != total) throw new IOException("incomplete download");
        return target;
    }

    public static string Install(string downloaded)
    {
        var current = Environment.ProcessPath;
        if (string.IsNullOrEmpty(current)) throw new InvalidOperationException("cannot locate the running executable");
        if (!File.Exists(downloaded)) throw new FileNotFoundException("downloaded file is missing", downloaded);
        if (new FileInfo(downloaded).Length < 1024 * 1024) throw new InvalidDataException("downloaded file is too small");

        var script = Path.Combine(Paths.Root, "update", "apply-update.cmd");
        var pid = Environment.ProcessId;

        var text = new StringBuilder();
        text.AppendLine("@echo off");
        text.AppendLine("setlocal");
        text.AppendLine($"set PID={pid}");
        text.AppendLine($"set NEW=\"{downloaded}\"");
        text.AppendLine($"set CUR=\"{current}\"");
        text.AppendLine(":wait");
        text.AppendLine("tasklist /FI \"PID eq %PID%\" /NH | find \"%PID%\" >nul");
        text.AppendLine("if not errorlevel 1 (");
        text.AppendLine("    ping -n 2 127.0.0.1 >nul");
        text.AppendLine("    goto wait");
        text.AppendLine(")");
        text.AppendLine("ping -n 2 127.0.0.1 >nul");
        text.AppendLine("copy /y %NEW% %CUR% >nul");
        text.AppendLine("if errorlevel 1 (");
        text.AppendLine("    echo Update failed, the original file is untouched.");
        text.AppendLine("    pause");
        text.AppendLine("    exit /b 1");
        text.AppendLine(")");
        text.AppendLine("del /q %NEW% >nul 2>&1");
        text.AppendLine("start \"\" %CUR%");
        text.AppendLine("del /q \"%~f0\" >nul 2>&1");

        File.WriteAllText(script, text.ToString(), Encoding.Default);

        Process.Start(new ProcessStartInfo
        {
            FileName = script,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        return script;
    }

    private static string Short(string s)
    {
        s = s.Replace("\r", " ").Replace("\n", " ").Trim();
        return s.Length > 180 ? s[..180] : s;
    }
}
