using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.DesktopHost.Ipc;

namespace TimeTrack.DesktopHost.UI;

/// <summary>
/// Manages the floating status bar lifecycle:
/// - Shows the bar when MainForm is minimized
/// - Hides the bar when MainForm is restored
/// - Refreshes data every 1 second via IPC queries
/// </summary>
public sealed class FloatingStatusBarManager : IDisposable
{
    private readonly MainForm _mainForm;
    private readonly IIpcClient _ipcClient;
    private readonly ILogger<FloatingStatusBarManager> _logger;

    private FloatingStatusBarForm? _bar;
    private System.Windows.Forms.Timer? _refreshTimer;
    private bool _disposed;

    public FloatingStatusBarManager(
        MainForm mainForm,
        IIpcClient ipcClient,
        ILogger<FloatingStatusBarManager> logger)
    {
        _mainForm = mainForm;
        _ipcClient = ipcClient;
        _logger = logger;

        _mainForm.WindowMinimized += OnWindowMinimized;
        _mainForm.WindowRestored += OnWindowRestored;

        _logger.LogInformation("FloatingStatusBarManager initialized");
    }

    private void OnWindowMinimized(object? sender, EventArgs e)
    {
        ShowBar();
    }

    private void OnWindowRestored(object? sender, EventArgs e)
    {
        HideBar();
    }

    private void ShowBar()
    {
        if (_bar is { Visible: true, IsDisposed: false })
            return;

        _logger.LogDebug("Showing floating status bar");

        _bar = new FloatingStatusBarForm();
        _bar.RestoreRequested += OnRestoreRequested;
        _bar.FormClosed += (_, _) =>
        {
            _refreshTimer?.Stop();
            _bar = null;
        };

        // First data load
        _ = RefreshDataAsync();

        _bar.Show();

        // Start 1-second refresh timer
        _refreshTimer?.Dispose();
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _refreshTimer.Tick += OnRefreshTick;
        _refreshTimer.Start();
    }

    private void HideBar()
    {
        _logger.LogDebug("Hiding floating status bar");

        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
        _refreshTimer = null;

        if (_bar is { IsDisposed: false })
        {
            _bar.RestoreRequested -= OnRestoreRequested;
            _bar.Close();
        }
        _bar = null;
    }

    private void OnRestoreRequested(object? sender, EventArgs e)
    {
        _mainForm.ShowWindow();
    }

    private async void OnRefreshTick(object? sender, EventArgs e)
    {
        await RefreshDataAsync();
    }

    private async Task RefreshDataAsync()
    {
        if (_bar is null or { IsDisposed: true }) return;
        if (!_ipcClient.IsConnected) return;

        try
        {
            // Query summary and tracking state in parallel
            var summaryTask = _ipcClient.SendQueryAsync("getTodaySummary");
            var stateTask = _ipcClient.SendQueryAsync("getTrackingState");
            await Task.WhenAll(summaryTask, stateTask);

            var summary = summaryTask.Result;
            var state = stateTask.Result;

            long activeSeconds = 0;
            long productiveSeconds = 0;
            int focusScore = 0;
            string? focusState = null;
            string? focusMode = null;
            long? focusRemainingMs = null;
            int? cycleNumber = null;

            // Parse summary
            if (summary.Success && summary.Data.HasValue)
            {
                var d = summary.Data.Value;
                if (d.TryGetProperty("totalDuration", out var td)) activeSeconds = td.GetInt64();
                if (d.TryGetProperty("productiveTime", out var pt)) productiveSeconds = pt.GetInt64();
                if (d.TryGetProperty("focusScore", out var fs)) focusScore = fs.GetInt32();
            }

            // Parse tracking state
            bool isTracking = false, isPaused = false;
            if (state.Success && state.Data.HasValue)
            {
                var d = state.Data.Value;
                if (d.TryGetProperty("isTracking", out var it)) isTracking = it.GetBoolean();
                if (d.TryGetProperty("isPaused", out var ip)) isPaused = ip.GetBoolean();
                if (d.TryGetProperty("focusModeState", out var fms)) focusState = fms.GetString();
                if (d.TryGetProperty("focusModeMode", out var fmm)) focusMode = fmm.GetString();
                if (d.TryGetProperty("focusRemainingMs", out var frm)) focusRemainingMs = frm.GetInt64();
            }

            // If focus mode is active, get cycle number
            if (focusState is "FocusRunning" or "BreakRunning")
            {
                var focusDetail = await _ipcClient.SendQueryAsync("getFocusModeState");
                if (focusDetail.Success && focusDetail.Data.HasValue)
                {
                    var d = focusDetail.Data.Value;
                    if (d.TryGetProperty("cycleNumber", out var cn)) cycleNumber = cn.GetInt32();
                }
            }

            // Update the bar on the UI thread
            if (_bar is { IsDisposed: false })
            {
                _bar.UpdateTrackingState(isTracking, isPaused);
                _bar.UpdateData(activeSeconds, productiveSeconds, focusScore,
                    focusState, focusMode, focusRemainingMs, cycleNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error refreshing floating status bar data");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _mainForm.WindowMinimized -= OnWindowMinimized;
        _mainForm.WindowRestored -= OnWindowRestored;
        HideBar();
    }
}
