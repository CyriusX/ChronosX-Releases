using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Agent.Contracts.Providers;

namespace TimeTrack.Agent.Infrastructure.Providers.Windows;

/// <summary>
/// Implementation of IIdleDetector using GetLastInputInfo via P/Invoke
/// </summary>
public sealed class WindowsIdleDetector : IIdleDetector
{
    private readonly ILogger<WindowsIdleDetector> _logger;
    private readonly IdleDetectorOptions _options;

    // Sleep detection: compare wall clock advance vs tick count advance.
    // TickCount64 does NOT advance during system sleep; DateTime.UtcNow does.
    // If wall advanced significantly more than ticks, a sleep occurred.
    private long _lastKnownTick = -1;
    private DateTime _lastKnownWallTime = DateTime.MinValue;
    private const long SleepDetectionThresholdMs = 30_000; // 30s discrepancy = sleep

    /// <summary>
    /// Fired when idle state changes
    /// </summary>
    public event EventHandler<IdleStateChangedEventArgs>? IdleStateChanged;

    public WindowsIdleDetector(
        ILogger<WindowsIdleDetector> logger,
        IOptions<IdleDetectorOptions>? options = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? new IdleDetectorOptions();
    }

    /// <inheritdoc />
    public Task<TimeSpan?> GetIdleTimeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var idleTime = GetIdleTimeInternal();

            if (idleTime.HasValue)
            {
                _logger.LogDebug("Current idle time: {IdleTime}", idleTime.Value);
            }

            return Task.FromResult(idleTime);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting idle time");
            return Task.FromResult<TimeSpan?>(null);
        }
    }

    /// <inheritdoc />
    public async Task<bool> IsIdleAsync(TimeSpan threshold, CancellationToken cancellationToken = default)
    {
        var idleTime = await GetIdleTimeAsync(cancellationToken);

        if (!idleTime.HasValue)
        {
            _logger.LogWarning("Could not determine idle time, assuming not idle");
            return false;
        }

        var isIdle = idleTime.Value >= threshold;

        _logger.LogDebug(
            "IsIdle check: {IsIdle} (idle: {IdleTime}, threshold: {Threshold})",
            isIdle,
            idleTime.Value,
            threshold);

        return isIdle;
    }

    /// <summary>
    /// Gets idle time using GetLastInputInfo.
    /// Returns null if a sleep/wake transition was just detected — the caller should
    /// treat null as "state unknown" and handle the gap separately.
    /// </summary>
    private TimeSpan? GetIdleTimeInternal()
    {
        var lastInputInfo = new LASTINPUTINFO
        {
            cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>()
        };

        if (!GetLastInputInfo(ref lastInputInfo))
        {
            var error = Marshal.GetLastWin32Error();
            _logger.LogWarning("GetLastInputInfo failed with error: {Error}", error);
            return null;
        }

        var currentTick = Environment.TickCount64;
        var nowWall = DateTime.UtcNow;

        // Sleep detection: TickCount64 freezes during sleep; DateTime.UtcNow does not.
        // If wall clock advanced significantly more than the tick counter since the last
        // call, the system was suspended and just woke up. Return null so TrackingWorker
        // can record the gap as an idle/sleep period instead of computing a garbage value.
        if (_lastKnownTick >= 0)
        {
            var tickAdvanceMs = currentTick - _lastKnownTick;
            var wallAdvanceMs = (long)(nowWall - _lastKnownWallTime).TotalMilliseconds;

            if (wallAdvanceMs - tickAdvanceMs > SleepDetectionThresholdMs)
            {
                _logger.LogInformation(
                    "Sleep/wake detected: wall advanced {Wall}ms but ticks only {Ticks}ms. Returning null to signal sleep.",
                    wallAdvanceMs, tickAdvanceMs);

                _lastKnownTick = currentTick;
                _lastKnownWallTime = nowWall;
                return null;
            }
        }

        _lastKnownTick = currentTick;
        _lastKnownWallTime = nowWall;

        // GetLastInputInfo returns ticks since system start (as uint).
        // Use unchecked subtraction to handle overflow correctly.
        var currentTickU = (ulong)currentTick;
        var lastInputTick = (ulong)lastInputInfo.dwTime;

        ulong idleMilliseconds;
        if (currentTickU >= lastInputTick)
        {
            idleMilliseconds = currentTickU - lastInputTick;
        }
        else
        {
            // Overflow: wrap around from max uint32 back to 0
            idleMilliseconds = (uint.MaxValue - lastInputTick) + currentTickU + 1;
        }

        _logger.LogDebug("Idle calculation: current={Current}, lastInput={LastInput}, idle={Idle}ms",
            currentTickU, lastInputTick, idleMilliseconds);

        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }

    #region P/Invoke

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    #endregion
}

/// <summary>
/// Configuration options for IdleDetector
/// </summary>
public sealed class IdleDetectorOptions
{
    /// <summary>
    /// Default idle threshold in seconds (3 minutes)
    /// </summary>
    public int DefaultThresholdSeconds { get; set; } = 300;

    /// <summary>
    /// Minimum threshold allowed (60 seconds)
    /// </summary>
    public int MinThresholdSeconds { get; set; } = 60;

    /// <summary>
    /// Maximum threshold allowed. No practical upper bound — a user tracking a 20-hour session
    /// should be able to set idle detection to any reasonable value.
    /// </summary>
    public int MaxThresholdSeconds { get; set; } = 86400; // 24 hours

    /// <summary>
    /// Polling interval in milliseconds when monitoring
    /// </summary>
    public int PollingIntervalMs { get; set; } = 2000;

    /// <summary>
    /// Validates and returns a threshold within allowed range
    /// </summary>
    public TimeSpan GetValidThreshold(TimeSpan? requested = null)
    {
        var seconds = requested?.TotalSeconds ?? DefaultThresholdSeconds;

        if (seconds < MinThresholdSeconds)
            seconds = MinThresholdSeconds;
        else if (seconds > MaxThresholdSeconds)
            seconds = MaxThresholdSeconds;

        return TimeSpan.FromSeconds(seconds);
    }
}
