using TimeTrack.Agent.Contracts.Notifications;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Tests.Notifications;

/// <summary>
/// Mock implementation of INotificationService for unit testing
///
/// SOLID:
/// - SRP: Apenas registra notificações para verificação em testes
/// - LSP: Pode ser usado onde INotificationService é esperado
/// - DIP: Permite testes sem dependência de Windows Toast
/// </summary>
public sealed class MockNotificationService : INotificationService
{
    private readonly List<AgentNotification> _sentNotifications = new();
    private readonly List<string> _clearedTags = new();
    private bool _clearAllCalled;

    /// <summary>
    /// List of all notifications that were sent
    /// </summary>
    public IReadOnlyList<AgentNotification> SentNotifications => _sentNotifications.AsReadOnly();

    /// <summary>
    /// List of tags that were cleared
    /// </summary>
    public IReadOnlyList<string> ClearedTags => _clearedTags.AsReadOnly();

    /// <summary>
    /// Whether ClearAllAsync was called
    /// </summary>
    public bool ClearAllCalled => _clearAllCalled;

    /// <summary>
    /// Controls whether SendAsync returns success or failure
    /// </summary>
    public bool ShouldSucceed { get; set; } = true;

    /// <summary>
    /// Gets the last notification that was sent, or null if none
    /// </summary>
    public AgentNotification? LastNotification => _sentNotifications.Count > 0
        ? _sentNotifications[^1]
        : null;

    /// <inheritdoc />
    public event EventHandler<NotificationActionEventArgs>? ActionInvoked;

    /// <inheritdoc />
    public Task<bool> SendAsync(AgentNotification notification, CancellationToken cancellationToken = default)
    {
        if (notification == null)
            throw new ArgumentNullException(nameof(notification));

        _sentNotifications.Add(notification);

        return Task.FromResult(ShouldSucceed);
    }

    /// <inheritdoc />
    public Task ClearAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be empty", nameof(tag));

        _clearedTags.Add(tag);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        _clearAllCalled = true;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Simulates a user clicking a notification action
    /// </summary>
    /// <param name="command">The IPC command to simulate</param>
    /// <param name="arguments">Optional arguments</param>
    /// <param name="tag">Optional tag</param>
    public void SimulateActionInvoked(string command, string? arguments = null, string? tag = null)
    {
        var args = new NotificationActionEventArgs
        {
            IpcCommand = command,
            Arguments = arguments,
            Tag = tag,
            Timestamp = DateTime.UtcNow
        };

        ActionInvoked?.Invoke(this, args);
    }

    /// <summary>
    /// Resets all tracking state
    /// </summary>
    public void Reset()
    {
        _sentNotifications.Clear();
        _clearedTags.Clear();
        _clearAllCalled = false;
        ShouldSucceed = true;
    }

    /// <summary>
    /// Asserts that a notification with the specified kind was sent
    /// </summary>
    public bool WasNotificationSent(NotificationKind kind)
    {
        return _sentNotifications.Any(n => n.Kind == kind);
    }

    /// <summary>
    /// Asserts that a notification with the specified tag was sent
    /// </summary>
    public bool WasNotificationSentWithTag(string tag)
    {
        return _sentNotifications.Any(n => n.Tag == tag);
    }

    /// <summary>
    /// Gets the count of notifications sent
    /// </summary>
    public int SentCount => _sentNotifications.Count;
}
