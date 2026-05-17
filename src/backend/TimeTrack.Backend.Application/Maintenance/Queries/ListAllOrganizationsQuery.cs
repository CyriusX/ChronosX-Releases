using MediatR;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Maintenance.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Maintenance.Queries;

public sealed record ListAllOrganizationsQuery() : IRequest<ListOrganizationsResponse>;

public sealed class ListAllOrganizationsQueryHandler
    : IRequestHandler<ListAllOrganizationsQuery, ListOrganizationsResponse>
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IUserRepository _userRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ICurrentUserContext _currentUser;

    public ListAllOrganizationsQueryHandler(
        IOrganizationRepository organizationRepository,
        IUserRepository userRepository,
        IDeviceRepository deviceRepository,
        ICurrentUserContext currentUser)
    {
        _organizationRepository = organizationRepository;
        _userRepository = userRepository;
        _deviceRepository = deviceRepository;
        _currentUser = currentUser;
    }

    public async Task<ListOrganizationsResponse> Handle(
        ListAllOrganizationsQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsPlatformAdmin)
            throw new UnauthorizedAccessException("Only platform admins can list all organizations");

        var orgs = (await _organizationRepository.GetAllAsync(cancellationToken))
            .OrderBy(o => o.Name)
            .ToList();

        var result = new List<OrganizationListItem>();

        foreach (var org in orgs)
        {
            var users = await _userRepository.GetByOrgIdAsync(org.Id, cancellationToken);
            var devices = await _deviceRepository.GetActiveByOrgIdAsync(org.Id, cancellationToken);

            result.Add(new OrganizationListItem
            {
                Id = org.Id,
                Name = org.Name,
                Slug = org.Slug,
                OrgType = org.OrgType.ToString(),
                Status = org.Status.ToString(),
                UserCount = users.Count(),
                DeviceCount = devices.Count(),
                CreatedAt = org.CreatedAt
            });
        }

        return new ListOrganizationsResponse
        {
            Organizations = result,
            TotalCount = result.Count
        };
    }
}
