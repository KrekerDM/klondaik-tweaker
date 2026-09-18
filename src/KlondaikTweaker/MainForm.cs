using System.Runtime.InteropServices;
using System.Text.Json;
using KlondaikTweaker.Core.Engine;
using KlondaikTweaker.Core.Modules;
using KlondaikTweaker.Host;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace KlondaikTweaker;

public sealed class MainForm : Form
{
    private const string Origin = "https://app.klondaik";
    private const int Border = 6;
    private const int MinWidth = 1120;
    private const int MinHeight = 720;

    private readonly WebView2 _web = new();
    private readonly System.Windows.Forms.Timer _metrics = new();
    private bool _ready;
    private volatile bool _metricsBusy;

    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    public MainForm()
    {
        Text = "Klondaik Tweaker";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(MinWidth, MinHeight);
        Size = new Size(1320, 860);
        BackColor = Color.FromArgb(11, 15, 20);
        DoubleBuffered = true;
        KeyPreview = true;
        try { Icon = new Icon(typeof(MainForm).Assembly.GetManifestResourceStream("KlondaikTweaker.Assets.app.ico") ?? Stream.Null); } catch { }

        _web.Dock = DockStyle.Fill;
        _web.DefaultBackgroundColor = Color.FromArgb(11, 15, 20);
        Controls.Add(_web);

        Load += OnLoad;
        FormClosing += OnClosing;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        try
        {
            int dark = 1;
            DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            int square = 1;
            DwmSetWindowAttribute(Handle, 33, ref square, sizeof(int));
        }
        catch { }
    }

    private async void OnLoad(object? sender, EventArgs e)
    {
        try
        {
            var options = new CoreWebView2EnvironmentOptions
            {
                AdditionalBrowserArguments = "--disable-background-timer-throttling --disable-features=msWebOOUI,msPdfOOUI,Translate --autoplay-policy=no-user-gesture-required"
            };
            var env = await CoreWebView2Environment.CreateAsync(null, Paths.WebCache, options);
            await _web.EnsureCoreWebView2Async(env);

            var core = _web.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.AreDevToolsEnabled = System.Diagnostics.Debugger.IsAttached;
            core.Settings.IsSwipeNavigationEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;

            core.AddWebResourceRequestedFilter(Origin + "/*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += OnResourceRequested;
            core.WebMessageReceived += OnWebMessage;
            core.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
                if (args.Uri.StartsWith("http", StringComparison.OrdinalIgnoreCase)) Core.Win.Sh.OpenExternal(args.Uri);
            };
            core.NavigationStarting += (_, args) =>
            {
                if (!args.Uri.StartsWith(Origin, StringComparison.OrdinalIgnoreCase)) args.Cancel = true;
            };
            core.ProcessFailed += (_, args) => Program.Log("webview process failed: " + args.ProcessFailedKind);
            core.NavigationCompleted += (_, _) =>
            {
                _ready = true;
                _metrics.Start();
                Push("window", new { max = WindowState == FormWindowState.Maximized });
            };

            _metrics.Interval = 1500;
            _metrics.Tick += PushMetrics;
            core.Navigate(Origin + "/index.html");
        }
        catch (Exception ex)
        {
            Program.Log(ex);
            MessageBox.Show("Не удалось запустить интерфейс: " + ex.Message, "Klondaik Tweaker");
            Close();
        }
    }

    private static readonly Dictionary<string, string> Mime = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "application/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".svg"] = "image/svg+xml; charset=utf-8",
        [".woff2"] = "font/woff2",
        [".ico"] = "image/x-icon"
    };

    private void OnResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        try
        {
            var uri = new Uri(e.Request.Uri);
            var path = uri.AbsolutePath.TrimStart('/');
            if (path.Length == 0) path = "index.html";
            var logical = "web/" + path;
            var stream = Res.Open(logical);
            if (stream is null)
            {
                e.Response = _web.CoreWebView2.Environment.CreateWebResourceResponse(null, 404, "Not Found", "");
                return;
            }
            var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            stream.Dispose();
            var ext = Path.GetExtension(path);
            var type = Mime.TryGetValue(ext, out var m) ? m : "application/octet-stream";
            var headers = $"Content-Type: {type}\r\nCache-Control: no-cache\r\nAccess-Control-Allow-Origin: *";
            e.Response = _web.CoreWebView2.Environment.CreateWebResourceResponse(ms, 200, "OK", headers);
        }
        catch (Exception ex)
        {
            Program.Log(ex);
        }
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string raw;
        try { raw = e.TryGetWebMessageAsString(); }
        catch { return; }

        JsonDocument doc;
        try { doc = JsonDocument.Parse(raw); }
        catch { return; }

        var root = doc.RootElement;
        var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
        var method = root.TryGetProperty("method", out var mEl) ? mEl.GetString() ?? "" : "";
        JsonElement? payload = root.TryGetProperty("payload", out var pEl) ? pEl.Clone() : null;
        doc.Dispose();

        if (method == "window.drag") { StartDrag(); return; }
        if (method == "window.minimize") { WindowState = FormWindowState.Minimized; return; }
        if (method == "window.maximize") { ToggleMax(); return; }
        if (method == "window.close") { Close(); return; }

        Task.Run(() =>
        {
            object? result = null;
            string? error = null;
            try { result = Api.Handle(method, payload, Push); }
            catch (Exception ex)
            {
                error = ex.Message;
                Program.Log(ex);
            }
            var envelope = JsonSerializer.Serialize(new { id, ok = error is null, result, error }, Store.Options);
            try
            {
                if (!IsDisposed) BeginInvoke(() => Send(envelope));
            }
            catch { }
        });
    }

    private void Send(string json)
    {
        try { _web.CoreWebView2?.PostWebMessageAsString(json); }
        catch { }
    }

    private void Push(string channel, object data)
    {
        var json = JsonSerializer.Serialize(new { evt = channel, data }, Store.Options);
        try
        {
            if (!IsDisposed) BeginInvoke(() => Send(json));
        }
        catch { }
    }

    private void PushMetrics(object? sender, EventArgs e)
    {
        if (!_ready || _metricsBusy) return;
        if (!Settings.Data.LiveMonitor) return;
        _metricsBusy = true;
        Task.Run(() =>
        {
            try { Push("metrics", HwMonitor.Read()); }
            catch { }
            finally { _metricsBusy = false; }
        });
    }

    private void ToggleMax() => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;

    private void StartDrag()
    {
        if (WindowState == FormWindowState.Maximized) return;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, 0x2, 0);
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x0084;
        const int WM_GETMINMAXINFO = 0x0024;

        if (m.Msg == WM_NCHITTEST && WindowState == FormWindowState.Normal)
        {
            var pos = PointToClient(new Point(m.LParam.ToInt32() & 0xFFFF, m.LParam.ToInt32() >> 16));
            bool left = pos.X <= Border, right = pos.X >= ClientSize.Width - Border;
            bool top = pos.Y <= Border, bottom = pos.Y >= ClientSize.Height - Border;
            int hit = (left, right, top, bottom) switch
            {
                (true, _, true, _) => 13,
                (_, true, true, _) => 14,
                (true, _, _, true) => 16,
                (_, true, _, true) => 17,
                (true, _, _, _) => 10,
                (_, true, _, _) => 11,
                (_, _, true, _) => 12,
                (_, _, _, true) => 15,
                _ => 0
            };
            if (hit != 0)
            {
                m.Result = hit;
                return;
            }
        }

        if (m.Msg == WM_GETMINMAXINFO)
        {
            var screen = Screen.FromHandle(Handle);
            var info = Marshal.PtrToStructure<MinMaxInfo>(m.LParam);
            info.ptMaxPosition = new Point(screen.WorkingArea.Left - screen.Bounds.Left, screen.WorkingArea.Top - screen.Bounds.Top);
            info.ptMaxSize = new Point(screen.WorkingArea.Width, screen.WorkingArea.Height);
            info.ptMinTrackSize = new Point(MinWidth, MinHeight);
            Marshal.StructureToPtr(info, m.LParam, true);
            m.Result = IntPtr.Zero;
            return;
        }

        base.WndProc(ref m);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point ptReserved;
        public Point ptMaxSize;
        public Point ptMaxPosition;
        public Point ptMinTrackSize;
        public Point ptMaxTrackSize;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (_ready) Push("window", new { max = WindowState == FormWindowState.Maximized });
    }

    private void OnClosing(object? sender, FormClosingEventArgs e)
    {
        _metrics.Stop();
        try { GameBoost.RestoreIfStale(); } catch { }
    }
}
