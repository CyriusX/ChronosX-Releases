using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para resetar a senha usando token
/// </summary>
public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest<ResetPasswordResponse>;

public sealed class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, ResetPasswordResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordValidator _passwordValidator;
    private readonly ITokenService _tokenService;

    public ResetPasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IPasswordHasher passwordHasher,
        IPasswordValidator passwordValidator,
        ITokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _passwordHasher = passwordHasher;
        _passwordValidator = passwordValidator;
        _tokenService = tokenService;
    }

    public async Task<ResetPasswordResponse> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // Validate password complexity
        var validationResult = _passwordValidator.Validate(request.NewPassword);
        if (!validationResult.IsValid)
        {
            throw new ValidationException("Password", string.Join("; ", validationResult.Errors));
        }

        // Hash the token and find it
        var tokenHash = _tokenService.HashRefreshToken(request.Token);
        var resetToken = await _passwordResetTokenRepository.GetByTokenHashAsync(tokenHash, cancellationToken);

        if (resetToken == null || !resetToken.IsValid)
        {
            throw new ValidationException("Token", "Invalid or expired reset token");
        }

        // Get user
        var user = await _userRepository.GetByIdAsync(resetToken.UserId, cancellationToken);
        if (user == null)
        {
            throw new NotFoundException("User", resetToken.UserId);
        }

        // Update password
        var newPasswordHash = _passwordHasher.Hash(request.NewPassword);
        user.ChangePassword(newPasswordHash);

        // Mark token as used
        resetToken.MarkAsUsed();

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _passwordResetTokenRepository.UpdateAsync(resetToken, cancellationToken);

        return new ResetPasswordResponse
        {
            Message = "Password has been reset successfully"
        };
    }
}
