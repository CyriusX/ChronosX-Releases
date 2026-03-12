using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para solicitar reset de senha
/// </summary>
public sealed record ForgotPasswordCommand(string Email) : IRequest<ForgotPasswordResponse>;

public sealed class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, ForgotPasswordResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;

    public ForgotPasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        ITokenService tokenService,
        IEmailService emailService)
    {
        _userRepository = userRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _tokenService = tokenService;
        _emailService = emailService;
    }

    public async Task<ForgotPasswordResponse> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        // Always return success to prevent email enumeration
        if (user == null)
        {
            return new ForgotPasswordResponse
            {
                Message = "If the email exists, a password reset link has been sent"
            };
        }

        // Invalidate any existing reset tokens for this user
        await _passwordResetTokenRepository.InvalidateAllByUserIdAsync(user.Id, cancellationToken);

        // Generate reset token
        var resetToken = _tokenService.GenerateRefreshToken(); // Use same secure token generator
        var resetTokenHash = _tokenService.HashRefreshToken(resetToken);

        var tokenEntity = Domain.Entities.PasswordResetToken.Create(
            user.Id,
            resetTokenHash,
            TimeSpan.FromHours(1));

        await _passwordResetTokenRepository.AddAsync(tokenEntity, cancellationToken);

        // Send reset email
        await _emailService.SendPasswordResetEmailAsync(
            request.Email,
            resetToken,
            cancellationToken);

        return new ForgotPasswordResponse
        {
            Message = "If the email exists, a password reset link has been sent"
        };
    }
}
