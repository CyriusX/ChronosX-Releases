using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace TimeTrack.DesktopHost.UI;

/// <summary>
/// Manages the system tray icon and context menu
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly MainForm _mainForm;
    private readonly ILogger<TrayIconManager> _logger;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;

    private bool _isTrackingPaused;
    private bool _isDisposed;

    public TrayIconManager(
        MainForm mainForm,
        ILogger<TrayIconManager> logger)
    {
        _mainForm = mainForm;
        _logger = logger;

        _contextMenu = CreateContextMenu();
        _notifyIcon = CreateNotifyIcon();

        _mainForm.FormClosing += OnFormClosing;
    }

    private NotifyIcon CreateNotifyIcon()
    {
        var icon = new NotifyIcon
        {
            Text = "TimeTrack",
            Icon = CreateDefaultIcon(),
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        icon.DoubleClick += OnNotifyIconDoubleClick;

        return icon;
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var menu = new ContextMenuStrip();

        // Open/Show window
        var showItem = menu.Items.Add("Abrir TimeTrack", null, OnShowClick);
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

    private Icon CreateDefaultIcon()
    {
        // Create a simple icon programmatically
        // In production, this should be loaded from resources
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.FromArgb(15, 15, 15)); // Dark background
        using var brush = new SolidBrush(Color.FromArgb(99, 102, 241));
        graphics.FillEllipse(brush, 2, 2, 28, 28); // Purple circle
        graphics.DrawString("T", new Font("Arial", 14, FontStyle.Bold), Brushes.White, 8, 6);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private void OnShowClick(object? sender, EventArgs e)
    {
        _mainForm.ShowWindow();
    }

    private void OnPauseClick(object? sender, EventArgs e)
    {
        _isTrackingPaused = !_isTrackingPaused;
        UpdatePauseMenuItem();

        // Notify via IPC to pause/resume tracking
        SendTrackingStateChangeAsync();
    }

    private void UpdatePauseMenuItem()
    {
        var pauseItems = _contextMenu.Items.Find("pauseTracking", true);
        if (pauseItems.Length > 0)
        {
            pauseItems[0].Text = _isTrackingPaused ? "Retomar Tracking" : "Pausar Tracking";
        }

        // Update icon color based on state
        _notifyIcon.Icon = _isTrackingPaused
            ? CreatePausedIcon()
            : CreateDefaultIcon();
    }

    private Icon CreatePausedIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);

        graphics.Clear(Color.FromArgb(15, 15, 15));
        using var brush = new SolidBrush(Color.FromArgb(245, 158, 11));
        graphics.FillEllipse(brush, 2, 2, 28, 28); // Orange circle
        graphics.DrawString("T", new Font("Arial", 14, FontStyle.Bold), Brushes.White, 8, 6);

        return Icon.FromHandle(bitmap.GetHicon());
    }

    private async void SendTrackingStateChangeAsync()
    {
        try
        {
            // This would send IPC command to AgentService
            // await _ipcClient.SendCommandAsync(_isTrackingPaused ? "pauseTracking" : "resumeTracking");
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
            "TimeTrack Desktop v1.0.0\n\nSistema de rastreamento de tempo\n\n© 2026 CyriusX",
            "Sobre o TimeTrack",
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

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        // Clean up tray icon on form close
        if (!_isDisposed)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
        }
    }

    public void UpdateStatus(string status, Color? color = null)
    {
        _notifyIcon.Text = $"TimeTrack - {status}";

        if (color != null)
        {
            // Update icon based on status
            // This could be expanded to show different icons for different states
        }
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
    }
}
