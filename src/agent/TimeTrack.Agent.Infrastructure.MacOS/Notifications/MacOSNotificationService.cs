using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Notifications;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.MacOS.Notifications;

/// <summary>
/// Implementation of INotificationService for macOS using UserNotifications framework
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacOSNotificationService : INotificationService
{
    private readonly ILogger<MacOSNotificationService> _logger;
    private readonly Dictionary<string, AgentNotification> _pendingNotifications = new();
    private readonly object _lock = new();

    public event EventHandler<NotificationActionEventArgs>? ActionInvoked;

    public MacOSNotificationService(ILogger<MacOSNotificationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        RequestAuthorization();
    }

    /// <inheritdoc />
    public async Task<bool> SendAsync(AgentNotification notification, CancellationToken cancellationToken = default)
    {
        try
        {
            await Task.Run(() =>
            {
                var content = CreateNotificationContent(notification);
                if (content == IntPtr.Zero)
                    return false;

                var request = CreateNotificationRequest(notification, content);
                if (request == IntPtr.Zero)
                {
                    CFRelease(content);
                    return false;
                }

                if (notification.Tag != null)
                {
                    lock (_lock)
                    {
                        _pendingNotifications[notification.Tag] = notification;
                    }
                }

                SendNotification(request);

                CFRelease(request);
                CFRelease(content);

                return true;
            }, cancellationToken);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification");
            return false;
        }
    }

    /// <inheritdoc />
    public Task ClearAsync(string tag, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    _pendingNotifications.Remove(tag);
                }

                var identifier = GetNotificationIdentifier(tag);
                RemoveNotification(identifier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing notification with tag {Tag}", tag);
            }
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            try
            {
                lock (_lock)
                {
                    foreach (var tag in _pendingNotifications.Keys)
                    {
                        var identifier = GetNotificationIdentifier(tag);
                        RemoveNotification(identifier);
                    }
                    _pendingNotifications.Clear();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing all notifications");
            }
        }, cancellationToken);
    }

    public void HandleNotificationAction(string tag, string actionType)
    {
        AgentNotification? notification = null;
        lock (_lock)
        {
            if (tag != null && _pendingNotifications.TryGetValue(tag, out notification))
            {
                _pendingNotifications.Remove(tag);
            }
        }

        if (notification == null)
            return;

        var action = actionType == "primary" ? notification.PrimaryAction : notification.SecondaryAction;
        if (action != null)
        {
            ActionInvoked?.Invoke(this, new NotificationActionEventArgs
            {
                IpcCommand = action.IpcCommand,
                Arguments = action.IpcArguments,
                Tag = tag
            });
        }
    }

    private IntPtr CreateNotificationContent(AgentNotification notification)
    {
        var content = UNMutableNotificationContent_AllocInit();
        if (content == IntPtr.Zero)
            return IntPtr.Zero;

        try
        {
            var titleCf = CFStringCreate(notification.Title);
            var bodyCf = CFStringCreate(notification.Body);

            UNMutableNotificationContent_setTitle(content, titleCf);
            UNMutableNotificationContent_setBody(content, bodyCf);

            CFRelease(titleCf);
            CFRelease(bodyCf);

            if (notification.Kind != NotificationKind.Generic)
            {
                var categoryIdentifier = GetCategoryIdentifier(notification.Kind);
                UNMutableNotificationContent_setCategoryIdentifier(content, categoryIdentifier);

                var category = CreateNotificationCategory(notification.Kind, notification.PrimaryAction, notification.SecondaryAction);
                if (category != IntPtr.Zero)
                {
                    var categories = CFArrayCreate(1, category);
                    UNUserNotificationCenter_setNotificationCategories(categories);

                    CFRelease(categories);
                    CFRelease(category);
                }
            }

            return content;
        }
        catch
        {
            if (content != IntPtr.Zero)
                CFRelease(content);
            return IntPtr.Zero;
        }
    }

    private IntPtr CreateNotificationCategory(NotificationKind kind, NotificationAction? primary, NotificationAction? secondary)
    {
        var identifier = GetCategoryIdentifier(kind);
        var category = UNNotificationCategory_AllocInit(identifier);
        if (category == IntPtr.Zero)
            return IntPtr.Zero;

        try
        {
            var actions = new List<IntPtr>();

            if (primary != null)
            {
                var action = CreateNotificationAction(primary, "primary");
                if (action != IntPtr.Zero)
                    actions.Add(action);
            }

            if (secondary != null)
            {
                var action = CreateNotificationAction(secondary, "secondary");
                if (action != IntPtr.Zero)
                    actions.Add(action);
            }

            if (actions.Count > 0)
            {
                var actionsArray = CFArrayCreate(actions.Count, actions.ToArray());
                UNNotificationCategory_setActions(category, actionsArray);
                CFRelease(actionsArray);
            }

            return category;
        }
        catch
        {
            CFRelease(category);
            return IntPtr.Zero;
        }
    }

    private IntPtr CreateNotificationAction(NotificationAction action, string actionType)
    {
        return UNNotificationAction_AllocInit(action.Label, actionType);
    }

    private IntPtr CreateNotificationRequest(AgentNotification notification, IntPtr content)
    {
        var identifier = GetNotificationIdentifier(notification.Tag ?? Guid.NewGuid().ToString());

        IntPtr trigger = IntPtr.Zero;
        if (notification.ExpiresIn.HasValue)
        {
            trigger = CreateTimeIntervalTrigger(notification.ExpiresIn.Value);
        }

        return UNNotificationRequest_AllocInit(identifier, content, trigger);
    }

    private IntPtr CreateTimeIntervalTrigger(TimeSpan timeInterval)
    {
        var seconds = timeInterval.TotalSeconds;
        return UNTimeIntervalNotificationTrigger_TriggerWithTimeInterval(seconds, false);
    }

    private void SendNotification(IntPtr request)
    {
        UNUserNotificationCenter_AddNotificationRequest(request, IntPtr.Zero);
    }

    private void RemoveNotification(string identifier)
    {
        var identifiers = CFArrayCreate(1, CFStringCreate(identifier));
        UNUserNotificationCenter_removeDeliveredNotificationsWithIdentifiers(identifiers);
        CFRelease(identifiers);
    }

    private void RequestAuthorization()
    {
        try
        {
            UNUserNotificationCenter_requestAuthorization(0b111, IntPtr.Zero);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to request notification authorization");
        }
    }

    private static string GetNotificationIdentifier(string tag) => $"timetrack-{tag}";

    private static string GetCategoryIdentifier(NotificationKind kind) => $"timetrack-{kind.ToString().ToLowerInvariant()}";

    #region Native Interop

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern IntPtr UNMutableNotificationContent_AllocInit();

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern void UNMutableNotificationContent_setTitle(IntPtr content, IntPtr title);

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern void UNMutableNotificationContent_setBody(IntPtr content, IntPtr body);

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern void UNMutableNotificationContent_setCategoryIdentifier(IntPtr content, IntPtr identifier);

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern IntPtr UNNotificationAction_AllocInit(string title, string identifier);

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern IntPtr UNNotificationCategory_AllocInit(string identifier);

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern void UNNotificationCategory_setActions(IntPtr category, IntPtr actions);

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern IntPtr UNNotificationRequest_AllocInit(string identifier, IntPtr content, IntPtr trigger);

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern IntPtr UNTimeIntervalNotificationTrigger_TriggerWithTimeInterval(double timeInterval, bool repeats);

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern void UNUserNotificationCenter_AddNotificationRequest(IntPtr request, IntPtr error);

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern void UNUserNotificationCenter_removeDeliveredNotificationsWithIdentifiers(IntPtr identifiers);

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern void UNUserNotificationCenter_setNotificationCategories(IntPtr categories);

    [DllImport("/System/Library/Frameworks/UserNotifications.framework/UserNotifications")]
    private static extern void UNUserNotificationCenter_requestAuthorization(int options, IntPtr completion);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringCreate(string str);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFArrayCreate(int numValues, params IntPtr[] values);

    private static IntPtr CFStringCreate(string str)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        unsafe
        {
            fixed (byte* ptr = bytes)
            {
                return CFStringCreateWithCString(IntPtr.Zero, ptr, 0x08000100);
            }
        }
    }

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFStringCreateWithCString(IntPtr alloc, byte* cStr, int encoding);

    #endregion
}
