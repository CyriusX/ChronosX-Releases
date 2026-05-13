using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IInviteLinkRepository
{
    Task<OrgInviteLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<OrgInviteLink?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<List<OrgInviteLink>> ListActiveByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default);
    Task AddAsync(OrgInviteLink link, CancellationToken cancellationToken = default);
    Task UpdateAsync(OrgInviteLink link, CancellationToken cancellationToken = default);
}
