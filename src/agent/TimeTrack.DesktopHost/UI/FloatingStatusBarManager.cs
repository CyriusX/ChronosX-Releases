using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.DesktopHost.Ipc;

namespace TimeTrack.DesktopHost.UI;

/// <summary>
/// Manages the floating status bar lifecycle:
/// - Shows the bar when MainForm is minimized
/// - Hides the bar when MainForm is restored
/// - Refreshes data every 1 second via IPC queries + WebView2 timer state
/// </summary>
public sealed class FloatingStatusBarManager : IDisposable
{
    private readonly MainForm _mainForm;
    private readonly IIpcClient _ipcClient;
    private readonly ILogger<FloatingStatusBarManager> _logger;

    private FloatingStatusBarForm? _bar;
    private System.Windows.Forms.Timer? _refreshTimer;
    private bool _disposed;

    // Focus command mapping: bar command string → timerStore JS action
    private static readonly Dictionary<string, string> FocusActionMap = new()
    {
        ["pauseFocusMode"] = "pause",
        ["resumeFocusMode"] = "resume",
        ["stopFocusMode"] = "stop",
        ["skipBreak"] = "skip",
    };

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

    private async void OnWindowMinimized(object? sender, EventArgs e) => await ShowBarAsync();
    private void OnWindowRestored(object? sender, EventArgs e) => HideBar();

    private async Task ShowBarAsync()
    {
        if (_bar is { Visible: true, IsDisposed: false }) return;

        _logger.LogDebug("Showing floating status bar");

        _bar = new FloatingStatusBarForm();
        _bar.RestoreRequested += OnRestoreRequested;
        _bar.FocusCommandRequested += OnFocusCommandRequested;
        _bar.FormClosed += (_, _) => { _refreshTimer?.Stop(); _bar = null; };

        // Fetch data BEFORE showing so the bar renders at correct size immediately
        await RefreshDataAsync();
        if (_bar is null or { IsDisposed: true }) return; // window may have been restored while awaiting

        _bar.Show();

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
            _bar.FocusCommandRequested -= OnFocusCommandRequested;
            _bar.Close();
        }
        _bar = null;
    }

    private void OnRestoreRequested(object? sender, EventArgs e) => _mainForm.ShowWindow();

    private async void OnFocusCommandRequested(object? sender, string command)
    {
        try
        {
            // Map bar command to timerStore JS action name
            if (FocusActionMap.TryGetValue(command, out var jsAction))
            {
                _logger.LogInformation("Focus command from bar: {Command} → JS action: {Action}", command, jsAction);
                await _mainForm.ExecuteTimerActionAsync(jsAction);
                // Refresh immediately to reflect the change
                await Task.Delay(150); // small delay for JS store to update localStorage
                await RefreshDataAsync();
            }
            else
            {
                _logger.LogWarning("Unknown focus command: {Command}", command);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing focus command {Command}", command);
        }
    }

    private async void OnRefreshTick(object? sender, EventArgs e) => await RefreshDataAsync();

    private async Task RefreshDataAsync()
    {
        if (_bar is null or { IsDisposed: true }) return;

        try
        {
            // --- 1. Get tracking data from AgentService IPC ---
            long activeSeconds = 0, productiveSeconds = 0;
            int focusScore = 0;
            bool isTracking = false, isPaused = false;

            if (_ipcClient.IsConnected)
            {
                var summaryTask = _ipcClient.SendQueryAsync("getTodaySummary");
                var stateTask = _ipcClient.SendQueryAsync("getTrackingState");
                await Task.WhenAll(summaryTask, stateTask);

                if (summaryTask.Result.Success && summaryTask.Result.Data.HasValue)
                {
                    var d = summaryTask.Result.Data.Value;
                    if (d.TryGetProperty("totalDuration", out var td)) activeSeconds = td.GetInt64();
                    if (d.TryGetProperty("productiveTime", out var pt)) productiveSeconds = pt.GetInt64();
                    if (d.TryGetProperty("focusScore", out var fs)) focusScore = fs.GetInt32();
                }

                if (stateTask.Result.Success && stateTask.Result.Data.HasValue)
                {
                    var d = stateTask.Result.Data.Value;
                    if (d.TryGetProperty("isTracking", out var it)) isTracking = it.GetBoolean();
                    if (d.TryGetProperty("isPaused", out var ip)) isPaused = ip.GetBoolean();
                }
            }

            // --- 2. Get focus timer state from WebView2 (React timerStore) ---
            string? focusState = null;
            string? focusMode = null;
            long? focusRemainingMs = null;
            int? cycleNumber = null;

            var timerJson = await _mainForm.GetTimerStateFromWebViewAsync();
            if (timerJson != null)
            {
                try
                {
                    using var doc = JsonDocument.Parse(timerJson);
                    var t = doc.RootElement;

                    var phase = t.TryGetProperty("phase", out var ph) ? ph.GetString() : "idle";
                    var mode = t.TryGetProperty("mode", out var md) ? md.GetString() : "pomodoro";
                    var totalMs = t.TryGetProperty("totalMs", out var tm) ? tm.GetInt64() : 0;
                    var phaseStartedAt = t.TryGetProperty("phaseStartedAt", out var ps) ? ps.GetInt64() : 0;
                    var pausedAt = t.TryGetProperty("pausedAt", out var pa) ? pa.GetInt64() : 0;
                    var pausedElapsedMs = t.TryGetProperty("pausedElapsedMs", out var pe) ? pe.GetInt64() : 0;
                    var cycle = t.TryGetProperty("cycle", out var cy) ? cy.GetInt32() : 0;

                    if (phase is "focus" or "break" or "longBreak")
                    {
                        // Compute remaining time using wall-clock (same logic as React _tick)
                        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                        var reference = pausedAt > 0 ? pausedAt : now;
                        var elapsed = reference - phaseStartedAt - pausedElapsedMs;
                        var remaining = Math.Max(0, totalMs - elapsed);

                        focusMode = mode == "ultradian" ? "Ultradian" : "Pomodoro";
                        focusRemainingMs = remaining;
                        cycleNumber = cycle + 1; // 0-based → 1-based

                        if (pausedAt > 0)
                            focusState = "FocusPaused";
                        else if (phase == "focus")
                            focusState = "FocusRunning";
                        else
                            focusState = "BreakRunning";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error parsing timer state from WebView");
                }
            }

            // --- 3. Update the bar ---
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
