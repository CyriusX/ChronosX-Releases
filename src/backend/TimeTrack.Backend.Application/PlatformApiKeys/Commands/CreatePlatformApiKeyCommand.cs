using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.PlatformApiKeys.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.PlatformApiKeys.Commands;

public sealed record CreatePlatformApiKeyCommand(string Label) : IRequest<CreatePlatformApiKeyResponse>;

public sealed class CreatePlatformApiKeyCommandHandler
    : IRequestHandler<CreatePlatformApiKeyCommand, CreatePlatformApiKeyResponse>
{
    private readonly IPlatformApiKeyRepository _repository;
    private readonly IPlatformApiKeyHasher _hasher;

    public CreatePlatformApiKeyCommandHandler(
        IPlatformApiKeyRepository repository,
        IPlatformApiKeyHasher hasher)
    {
        _repository = repository;
        _hasher = hasher;
    }

    public async Task<CreatePlatformApiKeyResponse> Handle(
        CreatePlatformApiKeyCommand request,
        CancellationToken cancellationToken)
    {
        var label = request.Label?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(label))
            throw new ValidationException("label", "Label is required.");
        if (label.Length > 200)
            throw new ValidationException("label", "Label must be 200 characters or fewer.");

        var token = _hasher.GenerateToken();
        var keyHash = _hasher.HashToken(token);

        var entity = PlatformApiKey.Create(Guid.NewGuid(), label, keyHash);
        await _repository.AddAsync(entity, cancellationToken);

        return new CreatePlatformApiKeyResponse
        {
            Id = entity.Id,
            Label = entity.Label,
            CreatedAtUtc = entity.CreatedAtUtc,
            RevokedAtUtc = entity.RevokedAtUtc,
            LastUsedAtUtc = entity.LastUsedAtUtc,
            Token = token
        };
    }
}

