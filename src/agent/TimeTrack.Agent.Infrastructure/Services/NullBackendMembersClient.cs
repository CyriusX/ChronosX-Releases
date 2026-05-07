using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// No-op implementation of IBackendMembersClient for local/test mode (no backend).
/// </summary>
public sealed class NullBackendMembersClient : IBackendMembersClient
{
    public Task<MemberSummaryResult?> GetMySummaryAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<MemberSummaryResult?>(null);
}

