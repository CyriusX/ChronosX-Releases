using MediatR;
using TimeTrack.Backend.Application.PlatformApiKeys.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.PlatformApiKeys.Queries;

public sealed record ListPlatformApiKeysQuery : IRequest<IReadOnlyList<PlatformApiKeyDto>>;

public sealed class ListPlatformApiKeysQueryHandler
    : IRequestHandler<ListPlatformApiKeysQuery, IReadOnlyList<PlatformApiKeyDto>>
{
    private readonly IPlatformApiKeyRepository _repository;

    public ListPlatformApiKeysQueryHandler(IPlatformApiKeyRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<PlatformApiKeyDto>> Handle(
        ListPlatformApiKeysQuery request,
        CancellationToken cancellationToken)
    {
        var keys = await _repository.GetAllAsync(cancellationToken);

        return keys
            .Select(k => new PlatformApiKeyDto
            {
                Id = k.Id,
                Label = k.Label,
                CreatedAtUtc = k.CreatedAtUtc,
                RevokedAtUtc = k.RevokedAtUtc,
                LastUsedAtUtc = k.LastUsedAtUtc
            })
            .ToList();
    }
}

