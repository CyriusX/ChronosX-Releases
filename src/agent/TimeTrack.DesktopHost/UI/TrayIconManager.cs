using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using TimeTrack.DesktopHost.Ipc;

namespace TimeTrack.DesktopHost.UI;

/// <summary>
/// Manages the system tray icon and context menu
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
        _isTrackingPaused = !_isTrackingPaused;
        UpdatePauseMenuItem();

        SendTrackingStateChangeAsync();
    }

    private void UpdatePauseMenuItem()
    {
        var pauseItems = _contextMenu.Items.Find("pauseTracking", true);
        if (pauseItems.Length > 0)
        {
            pauseItems[0].Text = _isTrackingPaused ? "Retomar Tracking" : "Pausar Tracking";
        }

        // Update icon: paused shows orange fallback, active shows the real icon
        _notifyIcon.Icon = _isTrackingPaused
            ? CreateFallbackIcon(Color.FromArgb(245, 158, 11)) // Orange
            : LoadAppIcon();
    }

    private async void SendTrackingStateChangeAsync()
    {
        try
        {
            var command = _isTrackingPaused ? "pauseTracking" : "resumeTracking";
            await _ipcClient.SendCommandAsync(command);
            _logger.LogInformation("Tracking state changed to: {State}", _isTrackingPaused ? "Paused" : "Active");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing tracking state");
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
