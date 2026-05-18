using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.PlatformApiKeys.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.PlatformApiKeys.Commands;

public sealed record RevokePlatformApiKeyCommand(Guid ApiKeyId) : IRequest<PlatformApiKeyDto>;

public sealed class RevokePlatformApiKeyCommandHandler
    : IRequestHandler<RevokePlatformApiKeyCommand, PlatformApiKeyDto>
{
    private readonly IPlatformApiKeyRepository _repository;

    public RevokePlatformApiKeyCommandHandler(IPlatformApiKeyRepository repository)
    {
        _repository = repository;
    }

    public async Task<PlatformApiKeyDto> Handle(
        RevokePlatformApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(request.ApiKeyId, cancellationToken);
        if (entity is null)
        {
            throw new NotFoundException("PlatformApiKey", request.ApiKeyId);
        }

        entity.Revoke();
        await _repository.UpdateAsync(entity, cancellationToken);

        return new PlatformApiKeyDto
        {
            Id = entity.Id,
            Label = entity.Label,
            CreatedAtUtc = entity.CreatedAtUtc,
            RevokedAtUtc = entity.RevokedAtUtc,
            LastUsedAtUtc = entity.LastUsedAtUtc
        };
    }
}

