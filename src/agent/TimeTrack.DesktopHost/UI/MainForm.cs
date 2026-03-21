using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using TimeTrack.DesktopHost.Configuration;
using TimeTrack.DesktopHost.Ipc;

namespace TimeTrack.DesktopHost.UI;

/// <summary>
/// Main form hosting WebView2 and integrating with IPC
/// </summary>
public sealed class MainForm : Form
{
    // DWM API for dark title bar (Windows 10 1809+ / Windows 11)
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const int DWMWA_BORDER_COLOR = 34;

    private readonly DesktopHostSettings _settings;
    private readonly IIpcClient _ipcClient;
    private readonly WebViewBridge _bridge;
    private readonly ILogger<MainForm> _logger;

    private WebView2? _webView;
    private bool _isInitialized;
    private bool _isClosing;

    public WebViewBridge Bridge => _bridge;

    public MainForm(
        DesktopHostSettings settings,
        IIpcClient ipcClient,
        WebViewBridge bridge,
        ILogger<MainForm> logger)
    {
        _settings = settings;
        _ipcClient = ipcClient;
        _bridge = bridge;
        _logger = logger;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        _webView = new WebView2
        {
            Dock = DockStyle.Fill
        };

        _webView.CoreWebView2InitializationCompleted += OnWebViewInitialized;
        _webView.NavigationStarting += OnNavigationStarting;

        Controls.Add(_webView);

        // Form settings — fixed size at 80% of screen, centered, non-resizable
        Text = _settings.AppTitle;
        var screen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
        var w = (int)(screen.Width * 0.80);
        var h = (int)(screen.Height * 0.80);
        Size = new Size(w, h);
        MinimumSize = new Size(w, h);
        MaximumSize = new Size(w, h);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        FormClosing += OnFormClosing;

        // Dark title bar matching the app UI (#0b0d14)
        ApplyDarkTitleBar();
    }

    private void ApplyDarkTitleBar()
    {
        try
        {
            var hwnd = Handle;

            // Enable dark mode for the title bar
            int darkMode = 1;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));

            // Set caption color to match app background (#0b0d14 = RGB 11,13,20)
            int captionColor = 11 | (13 << 8) | (20 << 16); // COLORREF: 0x00140D0B
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

            // Set border color to subtle dark border
            int borderColor = 30 | (33 << 8) | (46 << 16); // rgba(30,33,46) ≈ card border
            DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));
        }
        catch
        {
            // DWM APIs may not be available on older Windows versions — ignore
        }
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        if (_settings.StartMinimized)
        {
            WindowState = FormWindowState.Minimized;
            Hide();
        }

        await InitializeWebView2Async();
    }

    private async Task InitializeWebView2Async()
    {
        if (_isInitialized || _webView == null)
            return;

        try
        {
            var bundlePath = GetUiBundlePath();
            _logger.LogInformation("Loading UI from: {Path}", bundlePath);

            // Create environment with user data folder
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "TimeTrack",
                "WebView2Cache");

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

            // Initialize WebView2
            await _webView.EnsureCoreWebView2Async(env);

            // Set source after initialization
            if (bundlePath.StartsWith("http"))
            {
                _webView.Source = new Uri(bundlePath);
            }
            else
            {
                _webView.Source = new Uri(bundlePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WebView2");
            MessageBox.Show(
                $"Failed to initialize WebView2: {ex.Message}\n\nPlease ensure WebView2 Runtime is installed.",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnWebViewInitialized(object? sender, CoreWebView2InitializationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            _isInitialized = true;
            _logger.LogInformation("WebView2 initialized successfully");

            var coreWebView = _webView!.CoreWebView2!;

#if DEBUG
            // Open DevTools in debug mode
            coreWebView.OpenDevToolsWindow();
#endif

            // Configure WebView2 settings
            coreWebView.Settings.IsScriptEnabled = true;
            coreWebView.Settings.AreDefaultScriptDialogsEnabled = true;
            coreWebView.Settings.IsWebMessageEnabled = true;
            coreWebView.Settings.AreDefaultContextMenusEnabled = false;

            // Add bridge object to JavaScript
            coreWebView.AddHostObjectToScript("timeTrackBridge", _bridge);

            // Handle all WebMessages from JavaScript (console capture + IPC bridge)
            coreWebView.WebMessageReceived += async (s, args) =>
            {
                var msg = args.TryGetWebMessageAsString();
                if (msg == null) return;

                // IPC bridge via postMessage (avoids COM proxy deadlocks)
                if (msg.StartsWith("{\"_ipc\":"))
                {
                    await HandleIpcPostMessageAsync(coreWebView, msg);
                    return;
                }

                // Console log capture
                _logger.LogInformation("[JS] {Message}", msg);
            };

            // Inject script to set up IPC bridge via postMessage (avoids COM proxy issues)
            // This MUST run BEFORE React loads, so we use AddScriptToExecuteOnDocumentCreatedAsync
            _ = coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (function() {
                    // Override console.log to send messages to C# for diagnostics
                    var origLog = console.log;
                    var origError = console.error;
                    var origWarn = console.warn;
                    function post(level, args) {
                        try {
                            chrome.webview.postMessage('[' + level + '] ' + Array.from(args).map(function(a) {
                                try { return typeof a === 'object' ? JSON.stringify(a) : String(a); }
                                catch(e) { return String(a); }
                            }).join(' '));
                        } catch(e) {}
                    }
                    console.log = function() { post('LOG', arguments); origLog.apply(console, arguments); };
                    console.error = function() { post('ERR', arguments); origError.apply(console, arguments); };
                    console.warn = function() { post('WARN', arguments); origWarn.apply(console, arguments); };

                    // IPC bridge via postMessage — avoids COM async proxy deadlocks.
                    // Pending requests are tracked by requestId.
                    var _nextId = 1;
                    var _pending = {};

                    // Called from C# via ExecuteScriptAsync when a response arrives
                    window._timeTrackIpcResponse = function(requestId, responseJson) {
                        var p = _pending[requestId];
                        if (p) {
                            delete _pending[requestId];
                            p(responseJson);
                        }
                    };

                    function ipcCall(type, name, payloadJson) {
                        return new Promise(function(resolve) {
                            var id = _nextId++;
                            _pending[id] = resolve;
                            chrome.webview.postMessage(JSON.stringify({
                                _ipc: true,
                                requestId: id,
                                type: type,
                                name: name,
                                payloadJson: payloadJson || null
                            }));
                        });
                    }

                    // Expose the bridge object that the React IpcService expects
                    window.timeTrackBridge = {
                        isConnected: true,
                        SendCommand: function(command, payloadJson) { return ipcCall('command', command, payloadJson); },
                        SendQuery: function(query, payloadJson) { return ipcCall('query', query, payloadJson); }
                    };

                    console.log('[DesktopHost] Bridge injected successfully (postMessage mode)');
                })();
            ");

            // Subscribe to IPC events
            _ipcClient.EventReceived += OnIpcEventReceived;
            _ipcClient.ConnectionStateChanged += OnConnectionStateChanged;

            _logger.LogInformation("Bridge setup complete, IPC client connected: {IsConnected}", _ipcClient.IsConnected);
        }
        else
        {
            _logger.LogError("WebView2 initialization failed: {Error}", e.InitializationException);
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        // Block external navigation for security
        if (!string.IsNullOrEmpty(e.Uri))
        {
            var uri = new Uri(e.Uri);

            // Allow localhost and file:// protocols
            if (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "file")
            {
                _logger.LogWarning("Blocked navigation to unsupported protocol: {Uri}", e.Uri);
                e.Cancel = true;
                return;
            }

            // Block external domains in production
#if !DEBUG
            if (uri.Host != "localhost" && uri.Scheme != "file")
            {
                _logger.LogWarning("Blocked external navigation to: {Uri}", e.Uri);
                e.Cancel = true;
            }
#endif
        }
    }

    /// <summary>
    /// Handles IPC requests sent via chrome.webview.postMessage from JavaScript.
    /// Avoids the COM async proxy deadlock by using postMessage + ExecuteScriptAsync.
    /// </summary>
    private async Task HandleIpcPostMessageAsync(CoreWebView2 coreWebView, string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var requestId = root.GetProperty("requestId").GetInt32();
            var type = root.GetProperty("type").GetString() ?? "";
            var name = root.GetProperty("name").GetString() ?? "";
            var payloadJson = root.TryGetProperty("payloadJson", out var pj) && pj.ValueKind == JsonValueKind.String
                ? pj.GetString() : null;

            _logger.LogInformation("IPC via postMessage: type={Type}, name={Name}, requestId={RequestId}", type, name, requestId);

            string responseJson;
            if (type == "command")
            {
                responseJson = await _bridge.SendCommand(name, payloadJson).ConfigureAwait(false);
            }
            else
            {
                responseJson = await _bridge.SendQuery(name, payloadJson).ConfigureAwait(false);
            }

            // Send response back to JavaScript via ExecuteScriptAsync (must be on UI thread)
            var escapedResponse = responseJson.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r");
            var script = $"window._timeTrackIpcResponse?.({requestId}, '{escapedResponse}')";

            if (InvokeRequired)
            {
                BeginInvoke(new Action(async () =>
                {
                    try { await coreWebView.ExecuteScriptAsync(script); }
                    catch (Exception ex) { _logger.LogError(ex, "Error sending IPC response to JS"); }
                }));
            }
            else
            {
                await coreWebView.ExecuteScriptAsync(script);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling IPC postMessage");
        }
    }

    private void OnIpcEventReceived(object? sender, IpcEventArgs e)
    {
        _logger.LogInformation("OnIpcEventReceived: EventType={EventType}", e.EventType);

        // CRITICAL: IPC events come from background threads, but WebView2 can only be accessed from UI thread.
        // Use BeginInvoke to marshal the call back to the UI thread.
        if (InvokeRequired)
        {
            _logger.LogDebug("OnIpcEventReceived: Marshalling to UI thread via BeginInvoke");
            BeginInvoke(new Action(() => OnIpcEventReceived(sender, e)));
            return;
        }

        if (_webView?.CoreWebView2 == null)
        {
            _logger.LogWarning("OnIpcEventReceived: WebView not ready");
            return;
        }

        try
        {
            // Serialize payload to JSON
            var payloadJson = JsonSerializer.Serialize(e.Payload);
            _logger.LogInformation("OnIpcEventReceived: Forwarding to JavaScript, payload length={Length}", payloadJson.Length);

            // Call the global event handler that IpcService expects
            // This matches window.timeTrackHandleEvent(eventType, payloadJson)
            var script = $"window.timeTrackHandleEvent?.('{e.EventType}', '{EscapeJavaScriptString(payloadJson)}')";
            _logger.LogDebug("OnIpcEventReceived: Executing script: {Script}", script.Length > 200 ? script[..200] + "..." : script);

            _ = _webView.CoreWebView2.ExecuteScriptAsync(script);
            _logger.LogInformation("OnIpcEventReceived: Script executed successfully for {EventType}", e.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding IPC event to JavaScript");
        }
    }

    private static string EscapeJavaScriptString(string str)
    {
        return str.Replace("\\", "\\\\")
                  .Replace("'", "\\'")
                  .Replace("\"", "\\\"")
                  .Replace("\n", "\\n")
                  .Replace("\r", "\\r")
                  .Replace("\t", "\\t");
    }

    private void OnConnectionStateChanged(object? sender, bool isConnected)
    {
        // CRITICAL: IPC events come from background threads, but WebView2 can only be accessed from UI thread.
        // Use BeginInvoke to marshal the call back to the UI thread.
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => OnConnectionStateChanged(sender, isConnected)));
            return;
        }

        if (_webView?.CoreWebView2 == null)
            return;

        try
        {
            _ = _webView.CoreWebView2.ExecuteScriptAsync(
                $"window.timeTrackBridge?.onConnectionStateChanged?.({isConnected.ToString().ToLower()})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying connection state change");
        }
    }

    private string GetUiBundlePath()
    {
#if DEBUG
        // In debug mode, prefer development server for hot reload and proper ES module support
        const string devServerUrl = "http://localhost:5173";
        _logger.LogInformation("Debug mode: using development server at {Url}", devServerUrl);
        return devServerUrl;
#else
        // Look for UI bundle in standard locations (production)
        var executablePath = AppDomain.CurrentDomain.BaseDirectory;
        var possiblePaths = new[]
        {
            // Production: ui folder copied to output
            Path.Combine(executablePath, "ui", "dist", "index.html"),
            // Development: from bin/Debug/net8.0-windows to src/ui/timetrack-ui/dist (5 levels up)
            Path.Combine(executablePath, "..", "..", "..", "..", "..", "ui", "timetrack-ui", "dist", "index.html"),
            // Alternative paths for different build configurations
            Path.Combine(executablePath, "..", "..", "..", "..", "ui", "timetrack-ui", "dist", "index.html"),
            Path.Combine(executablePath, "..", "..", "..", "ui", "timetrack-ui", "dist", "index.html"),
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            _logger.LogDebug("Checking for UI bundle at: {Path}", fullPath);
            if (File.Exists(fullPath))
            {
                _logger.LogInformation("Found UI bundle at: {Path}", fullPath);
                return fullPath;
            }
        }

        // Fallback to development server
        _logger.LogWarning("UI bundle not found, falling back to development server");
        return "http://localhost:5174";
#endif
    }

    public void ShowWindow()
    {
        if (WindowState == FormWindowState.Minimized)
        {
            WindowState = FormWindowState.Normal;
        }

        Show();
        BringToFront();
        Activate();

        // Tell the React app to refresh all dashboard data immediately.
        // This ensures data shown after a tray-restore is never stale.
        NotifyAppVisible();
    }

    private void NotifyAppVisible()
    {
        if (_webView?.CoreWebView2 == null) return;
        try
        {
            _ = _webView.CoreWebView2.ExecuteScriptAsync(
                "window.dispatchEvent(new CustomEvent('app-visible'))");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch app-visible event to WebView2");
        }
    }

    public void CloseApplication()
    {
        _isClosing = true;
        Close();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);

        // Minimize to tray instead of taskbar
        if (WindowState == FormWindowState.Minimized)
        {
            Hide();
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isClosing && e.CloseReason == CloseReason.UserClosing)
        {
            // Hide to tray instead of closing
            e.Cancel = true;
            Hide();
            return;
        }

        // Cleanup on real exit
        if (_webView?.CoreWebView2 != null)
        {
            _ipcClient.EventReceived -= OnIpcEventReceived;
            _ipcClient.ConnectionStateChanged -= OnConnectionStateChanged;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _webView?.Dispose();
        }

        base.Dispose(disposing);
    }
}
