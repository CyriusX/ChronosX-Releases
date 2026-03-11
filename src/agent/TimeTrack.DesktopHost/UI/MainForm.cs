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

        // Form settings
        Text = _settings.AppTitle;
        Size = new Size(_settings.WindowWidth, _settings.WindowHeight);
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterScreen;
        FormClosing += OnFormClosing;
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

            // Add bridge object to JavaScript
            coreWebView.AddHostObjectToScript("timeTrackBridge", _bridge);

            // Inject script to map WebView2 host object to window.timeTrackBridge
            // This MUST run BEFORE React loads, so we use AddScriptToExecuteOnDocumentCreatedAsync
            _ = coreWebView.AddScriptToExecuteOnDocumentCreatedAsync(@"
                (function() {
                    if (chrome.webview && chrome.webview.hostObjects && chrome.webview.hostObjects.timeTrackBridge) {
                        window.timeTrackBridge = chrome.webview.hostObjects.timeTrackBridge;
                        console.log('[DesktopHost] Bridge injected to window.timeTrackBridge');
                    }
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

    private async void OnIpcEventReceived(object? sender, IpcEventArgs e)
    {
        if (_webView?.CoreWebView2 == null)
            return;

        try
        {
            var eventJson = JsonSerializer.Serialize(new
            {
                EventType = e.EventType,
                Payload = e.Payload
            });

            await _webView.CoreWebView2.ExecuteScriptAsync(
                $"window.timeTrackBridge?.onEvent?.({eventJson})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding IPC event to JavaScript");
        }
    }

    private void OnConnectionStateChanged(object? sender, bool isConnected)
    {
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
    }

    public void CloseApplication()
    {
        _isClosing = true;
        Close();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isClosing && e.CloseReason == CloseReason.UserClosing)
        {
            // Minimize to tray instead of closing
            e.Cancel = true;
            WindowState = FormWindowState.Minimized;
            Hide();
            return;
        }

        // Cleanup
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
