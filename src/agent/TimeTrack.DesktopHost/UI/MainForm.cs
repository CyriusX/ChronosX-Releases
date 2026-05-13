using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using TimeTrack.DesktopHost.Configuration;
using TimeTrack.DesktopHost.Ipc;
using TimeTrack.DesktopHost.Reporting;

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
    private readonly DesktopHostExceptionReporter _exceptionReporter;
    private readonly ILogger<MainForm> _logger;
    private readonly string _backendBaseUrl;
    private readonly HttpClient _proxyHttpClient;

    private WebView2? _webView;
    private bool _isInitialized;
    private bool _isClosing;
    private bool _devToolsEnabled;

    /// <summary>Raised when the main window is minimized or hidden to tray.</summary>
    public event EventHandler? WindowMinimized;
    /// <summary>Raised when the main window is restored from minimized/tray state.</summary>
    public event EventHandler? WindowRestored;

    /// <summary>Raised when the main window is minimized or hidden to tray.</summary>
    public event EventHandler? WindowMinimized;
    /// <summary>Raised when the main window is restored from minimized/tray state.</summary>
    public event EventHandler? WindowRestored;

    public WebViewBridge Bridge => _bridge;

    public MainForm(
        DesktopHostSettings settings,
        IIpcClient ipcClient,
        WebViewBridge bridge,
        DesktopHostExceptionReporter exceptionReporter,
        ILogger<MainForm> logger,
        IConfiguration configuration)
    {
        _settings = settings;
        _ipcClient = ipcClient;
        _bridge = bridge;
        _exceptionReporter = exceptionReporter;
        _logger = logger;
        _backendBaseUrl = (configuration["Agent:Sync:BackendUrl"] ?? "https://chronosx-dev-timetrack-api.gpoda0.easypanel.host").TrimEnd('/');
        _proxyHttpClient = new HttpClient(new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.All
        })
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

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
        LoadWindowIcon();
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

    private void LoadWindowIcon()
    {
        try
        {
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            var iconPath = Path.Combine(exeDir, "Resources", "app-icon.ico");
            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load window icon");
            _ = _exceptionReporter.ReportAsync(ex, "desktophost.loadWindowIcon");
        }
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
        catch (Exception ex)
        {
            // DWM APIs may not be available on older Windows versions — report anyway for diagnostics.
            _ = _exceptionReporter.ReportAsync(ex, "desktophost.applyDarkTitleBar");
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

            // Configure API proxy BEFORE navigation so /api/* requests are intercepted
            // immediately. Setting it up in OnWebViewInitialized causes a race condition
            // because the event fires after navigation has already started.
            ConfigureApiProxy(_webView.CoreWebView2!);

            // Set source after initialization
            if (bundlePath.StartsWith("http"))
            {
                _webView.Source = new Uri(bundlePath);
            }
            else
            {
                // Map the dist folder to a virtual host so the UI is served under a
                // proper https:// origin instead of file://. This avoids CORS issues
                // (file:// sends Origin: null which backends reject) and other
                // file:// protocol limitations.
                var distFolder = Path.GetDirectoryName(bundlePath)!;
                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "app.local",
                    distFolder,
                    CoreWebView2HostResourceAccessKind.Allow);


                // Append version as query parameter to bust WebView2 HTTP cache.
                // Vite hashes JS/CSS filenames, but the index.html itself can be
                // cached. The ?v= parameter forces a fresh fetch on each new version.
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0";
                _webView.Source = new Uri($"https://app.local/index.html?v={version}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WebView2");
            _ = _exceptionReporter.ReportAsync(ex, "desktophost.webview.initialize");
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
            coreWebView.NavigationCompleted += (_, _) =>
            {
                FocusWebViewContent();
                // After the first navigation completes, the injected bridge is available.
                // Push the current IPC connection state so the React app can reliably hydrate settings.
                OnConnectionStateChanged(this, _ipcClient.IsConnected);
            };

            // Configure WebView2 settings
            coreWebView.Settings.IsScriptEnabled = true;
            coreWebView.Settings.AreDefaultScriptDialogsEnabled = true;
            coreWebView.Settings.IsWebMessageEnabled = true;
            ApplyDevToolsAccess(false);

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
                    // Override the API base URL so the UI calls the configured backend
                    // (either local for dev or production). Without this, apiBase.ts
                    // would hardcode the production URL when running inside WebView2.
                    try {
                        window.__APP_CONFIG__ = Object.assign({}, window.__APP_CONFIG__ || {}, {
                            VITE_API_URL: '" + _backendBaseUrl + @"/api/v1'
                        });
                    } catch(e) {}

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
                        isConnected: false,
                        SendCommand: function(command, payloadJson) { return ipcCall('command', command, payloadJson); },
                        SendQuery: function(query, payloadJson) { return ipcCall('query', query, payloadJson); }
                    };

                    console.log('[DesktopHost] Bridge injected successfully (postMessage mode)');
                })();
            ");

            ConfigureApiProxy(coreWebView);

            // Subscribe to IPC events
            _ipcClient.EventReceived += OnIpcEventReceived;
            _ipcClient.ConnectionStateChanged += OnConnectionStateChanged;

            _logger.LogInformation("Bridge setup complete, IPC client connected: {IsConnected}", _ipcClient.IsConnected);

            // Push the initial connection state into the web UI (important after app restarts).
            OnConnectionStateChanged(this, _ipcClient.IsConnected);

            // Initial focus so login inputs can be typed into without requiring a manual click/reload.
            FocusWebViewContent();
        }
        else
        {
            _logger.LogError("WebView2 initialization failed: {Error}", e.InitializationException);
        }
    }

    private bool _proxyConfigured;

    private void ConfigureApiProxy(CoreWebView2 coreWebView)
    {
        if (_proxyConfigured) return;
        try
        {
            _proxyConfigured = true;
            coreWebView.AddWebResourceRequestedFilter("https://app.local/api/*", CoreWebView2WebResourceContext.All);
            coreWebView.WebResourceRequested += OnWebResourceRequested;
            _logger.LogInformation("API proxy enabled: https://app.local/api/* -> {Backend}", _backendBaseUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to configure API proxy");
            _ = _exceptionReporter.ReportAsync(ex, "desktophost.apiProxy.configure");
        }
    }

    private async void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        // Proxy /api/* from the embedded UI origin to the real backend.
        if (!e.Request.Uri.StartsWith("https://app.local/api/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            var incoming = new Uri(e.Request.Uri);
            var targetUri = new Uri($"{_backendBaseUrl}{incoming.PathAndQuery}");

            using var request = new HttpRequestMessage(new HttpMethod(e.Request.Method), targetUri);

            var auth = e.Request.Headers.GetHeader("Authorization");
            if (!string.IsNullOrWhiteSpace(auth))
            {
                request.Headers.TryAddWithoutValidation("Authorization", auth);
            }

            var accept = e.Request.Headers.GetHeader("Accept");
            if (!string.IsNullOrWhiteSpace(accept))
            {
                request.Headers.TryAddWithoutValidation("Accept", accept);
            }

            var contentType = e.Request.Headers.GetHeader("Content-Type");
            Stream? bodyStream = e.Request.Content;
            if (bodyStream != null)
            {
                using var ms = new MemoryStream();
                await bodyStream.CopyToAsync(ms);
                var bytes = ms.ToArray();
                request.Content = new ByteArrayContent(bytes);
                if (!string.IsNullOrWhiteSpace(contentType))
                {
                    request.Content.Headers.TryAddWithoutValidation("Content-Type", contentType);
                }
            }

            using var response = await _proxyHttpClient.SendAsync(request);

            var responseBytes = await response.Content.ReadAsByteArrayAsync();
            var responseStream = new MemoryStream(responseBytes);

            var headersBuilder = new StringBuilder();
            if (response.Content.Headers.ContentType != null)
            {
                headersBuilder.Append("Content-Type: ").Append(response.Content.Headers.ContentType.ToString()).Append("\r\n");
            }
            if (response.Headers.TryGetValues("Cache-Control", out var cache))
            {
                headersBuilder.Append("Cache-Control: ").Append(string.Join(", ", cache)).Append("\r\n");
            }

            e.Response = _webView?.CoreWebView2?.Environment?.CreateWebResourceResponse(
                responseStream,
                (int)response.StatusCode,
                response.ReasonPhrase ?? "",
                headersBuilder.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "API proxy failed for {Uri}", e.Request.Uri);
            _ = _exceptionReporter.ReportAsync(ex, "desktophost.apiProxy.request", new Dictionary<string, string?>
            {
                ["uri"] = e.Request.Uri
            });

            var body = Encoding.UTF8.GetBytes("{\"message\":\"DesktopHost API proxy error\",\"status\":502,\"code\":\"bad_gateway\"}");
            var stream = new MemoryStream(body);
            e.Response = _webView?.CoreWebView2?.Environment?.CreateWebResourceResponse(
                stream,
                502,
                "Bad Gateway",
                "Content-Type: application/json\r\n");
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void FocusWebViewContent()
    {
        if (_webView?.CoreWebView2 == null || _isClosing) return;

        try
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(FocusWebViewContent));
                return;
            }

            _webView.Focus();
            ActiveControl = _webView;

            try
            {
                if (_webView is { IsHandleCreated: true })
                {
                    _webView.Focus();
                }
            }
            catch
            {
                // Some WebView2 versions may not expose controller focus; ignore.
            }

            _ = _webView.CoreWebView2.ExecuteScriptAsync(@"
                try {
                    setTimeout(function() {
                        var el = document.querySelector('input[autofocus], input, textarea, [contenteditable=""true""]');
                        if (el && el.focus) el.focus();
                    }, 50);
                } catch(e) {}
            ");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to focus WebView2 content");
            _ = _exceptionReporter.ReportAsync(ex, "desktophost.webview.focus");
        }
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (string.IsNullOrEmpty(e.Uri)) return;
        var uri = new Uri(e.Uri);

        // Allow only http, https, file protocols
        if (uri.Scheme != "http" && uri.Scheme != "https" && uri.Scheme != "file")
        {
            _logger.LogWarning("Blocked navigation to unsupported protocol: {Uri}", e.Uri);
            e.Cancel = true;
            return;
        }

        // SPA fallback for app.local: non-file paths redirect to index.html
        if (uri.Host.Equals("app.local", StringComparison.OrdinalIgnoreCase))
        {
            var path = uri.AbsolutePath.Trim('/');
            var isStaticFile = path.StartsWith("assets/", StringComparison.OrdinalIgnoreCase)
                               || path.Equals("index.html", StringComparison.OrdinalIgnoreCase)
                               || path.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                               || path.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
                               || path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase)
                               || path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                               || path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                               || path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase)
                               || path.EndsWith(".woff", StringComparison.OrdinalIgnoreCase);

            if (!isStaticFile && !string.IsNullOrEmpty(path))
            {
                var query = !string.IsNullOrEmpty(uri.Query) ? uri.Query : "";
                e.Cancel = true;
                _webView!.CoreWebView2!.Navigate($"https://app.local/index.html{query}");
            }
            return;
        }

        // Block external domains in production (allow Stripe for checkout)
#if !DEBUG
        var allowedHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "localhost", "app.local",
            "checkout.stripe.com", "billing.stripe.com", "billing.stripe.me",
            "js.stripe.com", "fonts.googleapis.com"
        };

        if (!allowedHosts.Contains(uri.Host) && uri.Scheme != "file")
        {
            _logger.LogWarning("Blocked external navigation to: {Uri}", e.Uri);
            e.Cancel = true;
        }
#endif
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
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending IPC response to JS");
                        await _exceptionReporter.ReportAsync(ex, "desktophost.webview.executeScriptAsync.sendIpcResponse", new Dictionary<string, string?>
                        {
                            ["requestId"] = requestId.ToString(),
                            ["name"] = name
                        }).ConfigureAwait(false);
                    }
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
            await _exceptionReporter.ReportAsync(ex, "desktophost.ipc.postMessage", new Dictionary<string, string?>
            {
                ["jsonLength"] = json?.Length.ToString()
            }).ConfigureAwait(false);
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
            if (string.Equals(e.EventType, "devToolsAccessChanged", StringComparison.OrdinalIgnoreCase)
                && e.Payload.ValueKind == JsonValueKind.Object
                && e.Payload.TryGetProperty("devToolsEnabled", out var enabledProp)
                && enabledProp.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                ApplyDevToolsAccess(enabledProp.GetBoolean());
            }

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
            _ = _exceptionReporter.ReportAsync(ex, "desktophost.ipc.forwardEventToJs", new Dictionary<string, string?>
            {
                ["eventType"] = e.EventType
            });
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
            var isConnectedJs = isConnected.ToString().ToLowerInvariant();
            var payloadJson = $"{{\"isConnected\":{isConnectedJs}}}";

            // Keep window.timeTrackBridge.isConnected up-to-date so the React IPC client can read it on init.
            // Also push a connectionStateChanged event through the same channel used for IPC events
            // (window.timeTrackHandleEvent) so the UI reacts to reconnects.
            var script = $@"
                (function() {{
                    try {{
                        if (window.timeTrackBridge) {{
                            window.timeTrackBridge.isConnected = {isConnectedJs};
                        }}
                        if (window.timeTrackHandleEvent) {{
                            window.timeTrackHandleEvent('connectionStateChanged', '{EscapeJavaScriptString(payloadJson)}');
                        }}
                    }} catch(e) {{}}
                }})()";

            _ = _webView.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error notifying connection state change");
            _ = _exceptionReporter.ReportAsync(ex, "desktophost.ipc.connectionStateChanged", new Dictionary<string, string?>
            {
                ["isConnected"] = isConnected.ToString()
            });
        }

        if (isConnected)
            _ = Task.Run(SyncDevToolsAccessFromAgentAsync);
    }

    private void ApplyDevToolsAccess(bool enabled)
    {
        _devToolsEnabled = enabled;

        if (_webView?.CoreWebView2 == null)
            return;

        var settings = _webView.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = enabled;
        settings.AreBrowserAcceleratorKeysEnabled = enabled;
        settings.AreDevToolsEnabled = enabled;

        _logger.LogInformation("DevTools access applied to WebView2: Enabled={Enabled}", enabled);
    }

    private async Task SyncDevToolsAccessFromAgentAsync()
    {
        try
        {
            if (!_ipcClient.IsConnected)
                return;

            // If the UI loads before AgentService is connected, the initial getSettings call from React may fail
            // and never retry. DesktopHost also needs to robustly sync DevTools access on reconnect.
            IpcResponse? resp = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                resp = await _ipcClient.SendQueryAsync("getSettings");
                if (resp.Success && resp.Data is not null)
                    break;

                _logger.LogDebug(
                    "DevTools sync: getSettings failed (attempt {Attempt}/3). Error={Error}",
                    attempt,
                    resp.Error ?? "unknown");

                await Task.Delay(TimeSpan.FromMilliseconds(800 * attempt));
            }

            if (resp == null || !resp.Success || resp.Data is null)
                return;

            var data = resp.Data.Value;
            if (!data.TryGetProperty("devToolsEnabled", out var enabledProp)
                || enabledProp.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                return;

            var enabled = enabledProp.GetBoolean();

            if (data.TryGetProperty("devToolsEnabledUntilUtc", out var untilProp)
                && untilProp.ValueKind == JsonValueKind.String)
            {
                var raw = untilProp.GetString();
                if (!string.IsNullOrWhiteSpace(raw)
                    && DateTime.TryParse(raw, null, DateTimeStyles.RoundtripKind, out var until)
                    && until <= DateTime.UtcNow)
                {
                    enabled = false;
                }
            }

            _logger.LogInformation(
                "DevTools sync: agent settings fetched. Enabled={Enabled}, Until={UntilUtc}",
                enabled,
                data.TryGetProperty("devToolsEnabledUntilUtc", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null);

            if (InvokeRequired)
                BeginInvoke(new Action(() => ApplyDevToolsAccess(enabled)));
            else
                ApplyDevToolsAccess(enabled);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to sync DevTools access from AgentService");
            _ = _exceptionReporter.ReportAsync(ex, "desktophost.devTools.sync");
        }
    }

    private string GetUiBundlePath()
    {
#if DEBUG
        // In debug mode, check for a pre-built dist folder first so the app can run
        // without the Vite dev server. Falls back to the dev server if dist isn't present.
        var execDirDebug = AppDomain.CurrentDomain.BaseDirectory;
        var debugDistPaths = new[]
        {
            // Published layout
            Path.Combine(execDirDebug, "ui", "dist", "index.html"),
            // bin/Debug/net8.0-windows.../  (6 levels up to repo root)
            Path.Combine(execDirDebug, "..", "..", "..", "..", "..", "..", "src", "ui", "timetrack-ui", "dist", "index.html"),
            // bin/Debug/net8.0-windows.../win-x64/  (7 levels up to repo root)
            Path.Combine(execDirDebug, "..", "..", "..", "..", "..", "..", "..", "src", "ui", "timetrack-ui", "dist", "index.html"),
        };
        foreach (var p in debugDistPaths)
        {
            var full = Path.GetFullPath(p);
            if (File.Exists(full))
            {
                _logger.LogInformation("Debug mode: found dist bundle at {Path}", full);
                return full;
            }
        }
        const string devServerUrl = "http://localhost:5173";
        _logger.LogInformation("Debug mode: dist not found, falling back to dev server at {Url}", devServerUrl);
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

        // In production builds we must NOT silently fall back to a localhost dev server, which changes origin and
        // can make users appear "logged out" due to separate localStorage. Fail loudly instead.
        _logger.LogError("UI bundle not found in production paths. Aborting WebView2 initialization.");
        throw new FileNotFoundException("UI bundle not found. Please reinstall the app.");
#endif
    }

    public void ShowWindow()
    {
        // Show the form first (makes it visible but possibly behind other windows)
        Show();

        // Force normal state (must be after Show to avoid triggering OnResize while hidden)
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;

        // Use Win32 APIs to reliably bring the window to the foreground
        // BringToFront + Activate alone don't always work from a background context
        SetForegroundWindow(Handle);
        BringToFront();
        Activate();

        // Tell the React app to refresh all dashboard data immediately.
        // This ensures data shown after a tray-restore is never stale.
        NotifyAppVisible();

        WindowRestored?.Invoke(this, EventArgs.Empty);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    /// <summary>
    /// Reads the React timerStore state from the WebView2. Returns JSON or null.
    /// Used by FloatingStatusBarManager to show focus session on the bar.
    /// </summary>
    public async Task<string?> GetTimerStateFromWebViewAsync()
    {
        if (_webView?.CoreWebView2 == null) return null;
        try
        {
            // Read the Zustand timerStore persisted state from localStorage
            // The store key is 'xchronus-timer-store'
            var js = @"
                (function() {
                    try {
                        var raw = localStorage.getItem('xchronus-timer-store');
                        if (!raw) return JSON.stringify({phase:'idle'});
                        var parsed = JSON.parse(raw);
                        var s = parsed.state || {};
                        return JSON.stringify({
                            phase: s.phase || 'idle',
                            mode: s.mode || 'pomodoro',
                            remainingMs: s.remainingMs || 0,
                            totalMs: s.totalMs || 0,
                            cycle: s.cycle || 0,
                            phaseStartedAt: s.phaseStartedAt || 0,
                            pausedAt: s.pausedAt || 0,
                            pausedElapsedMs: s.pausedElapsedMs || 0
                        });
                    } catch(e) { return JSON.stringify({phase:'idle'}); }
                })()";
            var result = await _webView.CoreWebView2.ExecuteScriptAsync(js);
            // ExecuteScriptAsync returns a JSON-encoded string (wrapped in quotes)
            if (result != null && result.StartsWith("\""))
            {
                // Unescape the outer JSON string encoding
                return System.Text.Json.JsonSerializer.Deserialize<string>(result);
            }
            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Executes a focus mode action in the React timerStore via JavaScript.
    /// Actions: 'pause', 'resume', 'stop', 'skip'
    /// </summary>
    public async Task ExecuteTimerActionAsync(string action)
    {
        if (_webView?.CoreWebView2 == null) return;
        try
        {
            // Call the Zustand store actions directly
            var js = $@"
                (function() {{
                    try {{
                        var store = window.__TIMER_STORE__;
                        if (store) {{ store.getState().{action}(); return 'ok'; }}
                        return 'no-store';
                    }} catch(e) {{ return e.message; }}
            }})()";
            await _webView.CoreWebView2.ExecuteScriptAsync(js);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to execute timer action via WebView2: {Action}", action);
            await _exceptionReporter.ReportAsync(ex, "desktophost.timer.executeAction", new Dictionary<string, string?>
            {
                ["action"] = action
            }).ConfigureAwait(false);
        }
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
            _ = _exceptionReporter.ReportAsync(ex, "desktophost.webview.dispatchAppVisible");
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
            WindowMinimized?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isClosing && e.CloseReason == CloseReason.UserClosing)
        {
            // Hide to tray instead of closing
            e.Cancel = true;
            Hide();
            WindowMinimized?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Cleanup on real exit
        if (_webView?.CoreWebView2 != null)
        {
            _ipcClient.EventReceived -= OnIpcEventReceived;
            _ipcClient.ConnectionStateChanged -= OnConnectionStateChanged;
        }
    }

    /// <summary>
    /// Shows update progress in the WebView2 UI by dispatching a custom event.
    /// Called by NotificationEventHandler when updateProgress events are received.
    /// </summary>
    public void ShowUpdateProgress(string? stage, int percentage, string? message, string? targetVersion)
    {
        if (_webView?.CoreWebView2 == null)
        {
            _logger.LogWarning("Cannot show update progress: WebView not ready");
            return;
        }

        try
        {
            var payload = new
            {
                stage,
                percentage,
                message,
                targetVersion,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var payloadJson = JsonSerializer.Serialize(payload);
            var script = $"window.timeTrackHandleEvent?.('updateProgress', '{EscapeJavaScriptString(payloadJson)}')";

            if (InvokeRequired)
            {
                BeginInvoke(new Action(async () =>
                {
                    try { await _webView.CoreWebView2.ExecuteScriptAsync(script); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error sending update progress to WebView");
                        await _exceptionReporter.ReportAsync(ex, "desktophost.webview.updateProgress.send", new Dictionary<string, string?>
                        {
                            ["stage"] = stage,
                            ["percentage"] = percentage.ToString(),
                            ["targetVersion"] = targetVersion
                        }).ConfigureAwait(false);
                    }
                }));
            }
            else
            {
                _ = _webView.CoreWebView2.ExecuteScriptAsync(script);
            }

            _logger.LogDebug("Update progress sent to UI: Stage={Stage}, Percentage={Percentage}%", stage, percentage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing update progress");
            _ = _exceptionReporter.ReportAsync(ex, "desktophost.webview.updateProgress");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _webView?.Dispose();
            _proxyHttpClient.Dispose();
        }

        base.Dispose(disposing);
    }
}
