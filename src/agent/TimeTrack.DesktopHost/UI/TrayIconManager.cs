using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using TimeTrack.DesktopHost.Ipc;

namespace TimeTrack.DesktopHost.UI;

/// <summary>
/// Manages the system tray icon and context menu.
/// Listens to trackingStateChanged events so the tray stays in sync
/// regardless of whether pause/resume was triggered from the tray or the UI.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    private readonly MainForm _mainForm;
    private readonly IIpcClient _ipcClient;
    private readonly ILogger<TrayIconManager> _logger;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;

    // Track the current HICON so we can destroy it before replacing (GDI handle leak prevention)
    private IntPtr _currentIconHandle = IntPtr.Zero;

    private bool _isTrackingPaused;
    private bool _isDisposed;

    public TrayIconManager(
        MainForm mainForm,
        IIpcClient ipcClient,
        ILogger<TrayIconManager> logger)
    {
        _mainForm = mainForm;
        _ipcClient = ipcClient;
        _logger = logger;

        _contextMenu = CreateContextMenu();
        _notifyIcon = CreateNotifyIcon();

        // Subscribe to IPC events so the tray stays in sync when tracking
        // is paused/resumed from the UI (not just from the tray menu).
        _ipcClient.EventReceived += OnIpcEventReceived;
    }

    private void OnIpcEventReceived(object? sender, IpcEventArgs e)
    {
        if (e.EventType != "trackingStateChanged") return;

        try
        {
            var isPaused = false;
            var isTracking = true;

            if (e.Payload.TryGetProperty("isPaused", out var pausedEl))
                isPaused = pausedEl.GetBoolean();
            if (e.Payload.TryGetProperty("isTracking", out var trackingEl))
                isTracking = trackingEl.GetBoolean();

            var shouldShowPaused = !isTracking || isPaused;

            if (_isTrackingPaused != shouldShowPaused)
            {
                _isTrackingPaused = shouldShowPaused;

                // UI updates must happen on the UI thread
                if (_mainForm.InvokeRequired)
                    _mainForm.BeginInvoke(new Action(UpdatePauseMenuItem));
                else
                    UpdatePauseMenuItem();

                _logger.LogInformation("Tray synced with tracking state: {State}",
                    _isTrackingPaused ? "Paused" : "Active");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync tray with tracking state event");
        }
    }

    private NotifyIcon CreateNotifyIcon()
    {
        var icon = new NotifyIcon
        {
            Text = "ChronosX",
            Icon = LoadAppIcon(),
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        icon.DoubleClick += OnNotifyIconDoubleClick;

        return icon;
    }

    /// <summary>
    /// Loads the app icon from Resources/app-icon.ico next to the executable.
    /// Falls back to a generated icon if the file is not found.
    /// </summary>
    private Icon LoadAppIcon()
    {
        try
        {
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".";
            var iconPath = Path.Combine(exeDir, "Resources", "app-icon.ico");

            if (File.Exists(iconPath))
            {
                _logger.LogDebug("Loading tray icon from {Path}", iconPath);
                return new Icon(iconPath, 32, 32);
            }

            _logger.LogWarning("Icon file not found at {Path}, using fallback", iconPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load icon file, using fallback");
        }

        return CreateFallbackIcon(Color.FromArgb(74, 217, 255)); // Cyan (brand color)
    }

    private Icon CreateFallbackIcon(Color circleColor)
    {
        if (_currentIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_currentIconHandle);
            _currentIconHandle = IntPtr.Zero;
        }

        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.FromArgb(15, 15, 15));
        using var brush = new SolidBrush(circleColor);
        graphics.FillEllipse(brush, 2, 2, 28, 28);
        graphics.DrawString("C", new Font("Arial", 14, FontStyle.Bold), Brushes.White, 7, 6);

        _currentIconHandle = bitmap.GetHicon();
        return Icon.FromHandle(_currentIconHandle);
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new DarkToolStripRenderer(),
            ShowImageMargin = false,
            AutoSize = true
        };

        // Open/Show window
        var showItem = menu.Items.Add("Abrir ChronosX", null, OnShowClick);
        showItem.Font = new Font(showItem.Font, FontStyle.Bold);

        menu.Items.Add(new ToolStripSeparator());

        // Pause/Resume tracking
        var pauseItem = menu.Items.Add("Pausar Tracking", null, OnPauseClick);
        pauseItem.Name = "pauseTracking";

        menu.Items.Add(new ToolStripSeparator());

        // About
        menu.Items.Add("Sobre", null, OnAboutClick);

        menu.Items.Add(new ToolStripSeparator());

        // Exit
        menu.Items.Add("Sair", null, OnExitClick);

        return menu;
    }

    private void OnShowClick(object? sender, EventArgs e)
    {
        _mainForm.ShowWindow();
    }

    private void OnPauseClick(object? sender, EventArgs e)
    {
        _logger.LogInformation("OnPauseClick called, current state: {State}, IsConnected: {IsConnected}",
            _isTrackingPaused ? "Paused" : "Active", _ipcClient.IsConnected);

        if (!_ipcClient.IsConnected)
        {
            _logger.LogWarning("Cannot change tracking state: IPC not connected");
            _notifyIcon.ShowBalloonTip(3000, "Erro", "Não conectado ao AgentService", ToolTipIcon.Warning);
            return;
        }

        var previousState = _isTrackingPaused;
        _isTrackingPaused = !_isTrackingPaused;
        UpdatePauseMenuItem();

        SendTrackingStateChangeAsync(previousState);
    }

    private void UpdatePauseMenuItem()
    {
        var pauseItems = _contextMenu.Items.Find("pauseTracking", true);
        if (pauseItems.Length > 0)
        {
            pauseItems[0].Text = _isTrackingPaused ? "Retomar Tracking" : "Pausar Tracking";
        }
    }

    private async void SendTrackingStateChangeAsync(bool previousState)
    {
        try
        {
            IpcResponse response;
            if (_isTrackingPaused)
            {
                // Send pause with reason so the activity session is labeled correctly
                response = await _ipcClient.SendCommandAsync("pauseTracking", new { reason = "Tracking Stopped" });
            }
            else
            {
                response = await _ipcClient.SendCommandAsync("startTracking");
            }

            if (!response.Success)
            {
                // Revert state on failure
                _isTrackingPaused = previousState;
                UpdatePauseMenuItem();

                _logger.LogError("Failed to change tracking state: {Error}", response.Error);
                _notifyIcon.ShowBalloonTip(3000, "Erro", $"Falha ao alterar estado: {response.Error}", ToolTipIcon.Error);
                return;
            }

            _logger.LogInformation("Tracking state changed to: {State}", _isTrackingPaused ? "Paused" : "Active");
        }
        catch (Exception ex)
        {
            // Revert state on exception
            _isTrackingPaused = previousState;
            UpdatePauseMenuItem();

            _logger.LogError(ex, "Error changing tracking state");
            _notifyIcon.ShowBalloonTip(3000, "Erro", $"Erro ao alterar estado: {ex.Message}", ToolTipIcon.Error);
        }
    }

    private void OnAboutClick(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "ChronosX Desktop v1.0.0\n\nSistema de rastreamento de tempo e produtividade\n\n© 2026 ChronosX",
            "Sobre o ChronosX",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void OnExitClick(object? sender, EventArgs e)
    {
        var result = MessageBox.Show(
            "Deseja realmente sair da aplicação?\n\nO AgentService continuará em execução em segundo plano.",
            "Confirmar saída",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            _notifyIcon.Visible = false;
            _mainForm.CloseApplication();
        }
    }

    private void OnNotifyIconDoubleClick(object? sender, EventArgs e)
    {
        _mainForm.ShowWindow();
    }

    public void UpdateStatus(string status, Color? color = null)
    {
        _notifyIcon.Text = $"ChronosX - {status}";
    }

    public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        _ipcClient.EventReceived -= OnIpcEventReceived;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _contextMenu.Dispose();

        if (_currentIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_currentIconHandle);
            _currentIconHandle = IntPtr.Zero;
        }
    }
}
