using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.PlatformAdmin.Commands;

/// <summary>
/// Grants or revokes platform-admin status on a user. Platform admins
/// bypass all subscription/paywall checks — they are Chronos staff, not
/// paying customers. Only callable by another platform admin.
/// </summary>
public sealed record SetPlatformAdminCommand(Guid TargetUserId, bool IsPlatformAdmin) : IRequest<SetPlatformAdminResponse>;

public sealed class SetPlatformAdminResponse
{
    public Guid UserId { get; init; }
    public string Email { get; init; } = string.Empty;
    public bool IsPlatformAdmin { get; init; }
}

public sealed class SetPlatformAdminCommandHandler : IRequestHandler<SetPlatformAdminCommand, SetPlatformAdminResponse>
{
    private readonly IUserRepository _userRepository;

    public SetPlatformAdminCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<SetPlatformAdminResponse> Handle(SetPlatformAdminCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdUnfilteredAsync(request.TargetUserId, cancellationToken);
        if (user is null)
        {
            throw new NotFoundException("User", request.TargetUserId);
        }

        if (user.IsPlatformAdmin != request.IsPlatformAdmin)
        {
            user.SetPlatformAdmin(request.IsPlatformAdmin);
            await _userRepository.UpdateAsync(user, cancellationToken);
        }

        return new SetPlatformAdminResponse
        {
            UserId = user.Id,
            Email = user.Email,
            IsPlatformAdmin = user.IsPlatformAdmin
        };
    }
}
