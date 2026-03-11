using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Infrastructure.Providers.Windows;

namespace TimeTrack.DesktopHost;

public class MainForm : Form
{
    private readonly IActiveWindowProvider _activeWindowProvider;
    private readonly IIdleDetector _idleDetector;
    private readonly ILogger<MainForm> _logger;

    private Button _btnGetWindow;
    private Button _btnGetIdle;
    private TextBox _txtResult;
    private System.Windows.Forms.Timer _updateTimer;

    public MainForm(
        IActiveWindowProvider activeWindowProvider,
        IIdleDetector idleDetector,
        ILogger<MainForm> logger)
    {
        _activeWindowProvider = activeWindowProvider;
        _idleDetector = idleDetector;
        _logger = logger;

        InitializeComponent();
        StartUpdateTimer();
    }

    private void InitializeComponent()
    {
        Text = "TimeTrack Desktop - Teste";
        Width = 600;
        Height = 400;
        StartPosition = FormStartPosition.CenterScreen;

        var pnlButtons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            Height = 80,
            Dock = DockStyle.Bottom
        };

        _btnGetWindow = new Button { Text = "Get Active Window", Width = 150, Height = 40 };
        _btnGetIdle = new Button { Text = "Get Idle Time", Width = 150, Height = 40 };

        pnlButtons.Controls.Add(_btnGetWindow);
        pnlButtons.Controls.Add(_btnGetIdle);

        _txtResult = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10),
            ReadOnly = true
        };

        Controls.Add(pnlButtons);
        Controls.Add(_txtResult);

        _btnGetWindow.Click += async (s, e) => await OnGetActiveWindowClickAsync();
        _btnGetIdle.Click += async (s, e) => await OnGetIdleClickAsync();
    }

    private void StartUpdateTimer()
    {
        _updateTimer = new System.Windows.Forms.Timer();
        _updateTimer.Interval = 2000;
        _updateTimer.Tick += async (s, e) => await UpdateResultAsync();
        _updateTimer.Start();
    }

    private async Task OnGetActiveWindowClickAsync()
    {
        _txtResult.Clear();

        try
        {
            var window = await _activeWindowProvider.GetActiveWindowAsync();
            if (window != null)
            {
                _txtResult.AppendText($"[{DateTime.Now:HH:mm:ss}] Active Window:");
                _txtResult.AppendText($"  App: {window.DisplayName}");
                _txtResult.AppendText($"  Path: {window.ExePath}");
                _txtResult.AppendText($"  Title: {window.WindowTitle ?? "(no title)"}");
                _txtResult.AppendText($"  Hash: {window.ExePathHash[..16]}...");
                _txtResult.AppendText($"  Window Hash: {window.WindowHash?[..16] ?? "(none)"}");
                _txtResult.AppendText("");
            }
            else
            {
                _txtResult.AppendText($"[{DateTime.Now:HH:mm:ss}] No active window detected");
            }
        }
        catch (Exception ex)
        {
            _txtResult.AppendText($"[{DateTime.Now:HH:mm:ss}] Error: {ex.Message}");
        }
    }

    private async Task OnGetIdleClickAsync()
    {
        _txtResult.Clear();

        try
        {
            var idleTime = await _idleDetector.GetIdleTimeAsync();
            if (idleTime.HasValue)
            {
                _txtResult.AppendText($"[{DateTime.Now:HH:mm:ss}] Idle Time:");
                _txtResult.AppendText($"  {idleTime.Value.TotalSeconds:F0} seconds");
                _txtResult.AppendText($"  {idleTime.Value:hh\\:mm\\:ss}");
            }
            else
            {
                _txtResult.AppendText($"[{DateTime.Now:HH:mm:ss}] Unable to get idle time");
            }
        }
        catch (Exception ex)
        {
            _txtResult.AppendText($"[{DateTime.Now:HH:mm:ss}] Error: {ex.Message}");
        }
    }

    private async Task UpdateResultAsync()
    {
        try
        {
            var window = await _activeWindowProvider.GetActiveWindowAsync();
            var idleTime = await _idleDetector.GetIdleTimeAsync();

            _txtResult.Clear();
            _txtResult.AppendText($"[{DateTime.Now:HH:mm:ss}] Auto Update:");
            if (window != null)
            {
                _txtResult.AppendText($"  App: {window.DisplayName}");

                // Fix: proper null handling
                var idleSeconds = idleTime?.TotalSeconds ?? 0;
                var idleText = idleSeconds > 0 ? $"{idleSeconds:F0}s" : "unknown";
                _txtResult.AppendText($"  Idle: {idleText}");
            }
            else
            {
                _txtResult.AppendText("  No active window");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in update timer");
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _updateTimer?.Stop();
        base.OnFormClosing(e);
    }
}
