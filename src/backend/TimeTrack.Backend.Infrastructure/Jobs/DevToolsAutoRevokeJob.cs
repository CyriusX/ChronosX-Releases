using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Auto-revokes expired per-user DevTools access and pushes disable commands to all active devices
/// for the affected users.
/// </summary>
public sealed class DevToolsAutoRevokeJob : IDevToolsAutoRevokeJob
{
    private static readonly TimeSpan RemoteCommandTtl = TimeSpan.FromDays(7);

    private readonly TimeTrackDbContext _context;
    private readonly ILogger<DevToolsAutoRevokeJob> _logger;

    public DevToolsAutoRevokeJob(TimeTrackDbContext context, ILogger<DevToolsAutoRevokeJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var now = DateTime.UtcNow;

        var expiredUsers = await _context.Users
            .Where(u => u.DevToolsEnabledUntilUtc != null && u.DevToolsEnabledUntilUtc <= now)
            .ToListAsync();

        if (expiredUsers.Count == 0)
            return;

        var expiredUserIds = expiredUsers.Select(u => u.Id).ToList();

        var activeDevices = await _context.Devices
            .AsNoTracking()
            .Where(d => d.Status == DeviceStatus.Active && expiredUserIds.Contains(d.UserId))
            .Select(d => new { d.OrgId, d.Id, d.UserId })
            .ToListAsync();

        foreach (var user in expiredUsers)
        {
            user.SetDevToolsEnabledUntilUtc(null);
        }

        var payloadJson = JsonSerializer.Serialize(new
        {
            enabled = false,
            expiresAtUtc = (string?)null
        });

        var commands = activeDevices.Select(d => RemoteCommand.Create(
            d.OrgId,
            d.Id,
            commandType: "set_devtools",
            payloadJson: payloadJson,
            createdByUserId: Guid.Empty,
            ttl: RemoteCommandTtl)).ToList();

        if (commands.Count > 0)
            await _context.RemoteCommands.AddRangeAsync(commands);

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "DevToolsAutoRevokeJob revoked DevTools access for {UserCount} users and queued {CommandCount} disable commands",
            expiredUsers.Count,
            commands.Count);
    }
}

