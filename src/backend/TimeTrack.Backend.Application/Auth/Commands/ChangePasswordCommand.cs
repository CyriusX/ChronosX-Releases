using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para alterar senha do usuário logado
/// </summary>
public sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<ChangePasswordResponse>;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, ChangePasswordResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordValidator _passwordValidator;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPasswordValidator passwordValidator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _passwordValidator = passwordValidator;
    }

    public async Task<ChangePasswordResponse> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        // Validate new password complexity
        var validationResult = _passwordValidator.Validate(request.NewPassword);
        if (!validationResult.IsValid)
        {
            throw new ValidationException("Password", string.Join("; ", validationResult.Errors));
        }

        // Get user
        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User", request.UserId);
        }

        // Verify current password
        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            throw new ValidationException("CurrentPassword", "Current password is incorrect");
        }

        // Update password
        var newPasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.ChangePassword(newPasswordHash);

        await _userRepository.UpdateAsync(user, cancellationToken);

        return new ChangePasswordResponse
        {
            Message = "Password changed successfully"
        };
    }
}
