using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Agent.Contracts.Providers;

namespace TimeTrack.Agent.Infrastructure.MacOS.Providers;

/// <summary>
/// Implementation of IIdleDetector for macOS using CoreGraphics CGEventSource
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacOSIdleDetector : IIdleDetector
{
    private readonly ILogger<MacOSIdleDetector> _logger;
    private readonly IdleDetectorOptions _options;

    private long _lastKnownTick = -1;
    private DateTime _lastKnownWallTime = DateTime.MinValue;
    private const long SleepDetectionThresholdMs = 30_000;

    /// <summary>
    /// Fired when idle state changes
    /// </summary>
    public event EventHandler<IdleStateChangedEventArgs>? IdleStateChanged;

    public MacOSIdleDetector(
        ILogger<MacOSIdleDetector> logger,
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

    private TimeSpan? GetIdleTimeInternal()
    {
        try
        {
            var idleSeconds = CGEventSourceSecondsSinceLastEventType(
                (int)CGEventSourceStateID.CombinedSessionState,
                (int)CGEventType.AllEvents);

            if (idleSeconds < 0)
            {
                _logger.LogWarning("CGEventSourceSecondsSinceLastEventType returned negative value");
                return null;
            }

            var idleTime = TimeSpan.FromSeconds(idleSeconds);
            var currentTick = Environment.TickCount64;
            var nowWall = DateTime.UtcNow;

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

            return idleTime;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting idle time via CoreGraphics");
            return null;
        }
    }

    #region Native Interop

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern double CGEventSourceSecondsSinceLastEventType(int state, int eventType);

    private enum CGEventSourceStateID
    {
        CombinedSessionState = 0,
        PrivateSessionState = 1,
        HIDSystemState = 2
    }

    private enum CGEventType
    {
        LeftMouseDown = 1,
        LeftMouseUp = 2,
        RightMouseDown = 3,
        RightMouseUp = 4,
        MouseMoved = 5,
        LeftMouseDragged = 6,
        RightMouseDragged = 7,
        KeyDown = 10,
        KeyUp = 11,
        FlagsChanged = 12,
        ScrollWheel = 22,
        TabletPointer = 23,
        TabletProximity = 24,
        OtherMouseDown = 25,
        OtherMouseUp = 26,
        OtherMouseDragged = 27,
        AllEvents = -1
    }

    #endregion
}

/// <summary>
/// Configuration options for IdleDetector
/// </summary>
public sealed class IdleDetectorOptions
{
    public int DefaultThresholdSeconds { get; set; } = 300;

    public int MinThresholdSeconds { get; set; } = 60;

    public int MaxThresholdSeconds { get; set; } = 86400;

    public int PollingIntervalMs { get; set; } = 2000;

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
