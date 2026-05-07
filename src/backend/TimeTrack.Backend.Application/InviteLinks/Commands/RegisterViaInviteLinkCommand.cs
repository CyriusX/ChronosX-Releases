using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.InviteLinks.Commands;

public sealed record RegisterViaInviteLinkCommand(
    string Token,
    string DisplayName,
    string Email,
    string Password) : IRequest<LoginResponse>;

public sealed class RegisterViaInviteLinkCommandHandler : IRequestHandler<RegisterViaInviteLinkCommand, LoginResponse>
{
    private readonly IInviteLinkRepository _inviteLinkRepository;
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordValidator _passwordValidator;
    private readonly ITokenService _tokenService;
    private readonly ISubscriptionService _subscriptionService;

    public RegisterViaInviteLinkCommandHandler(
        IInviteLinkRepository inviteLinkRepository,
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IPasswordValidator passwordValidator,
        ITokenService tokenService,
        ISubscriptionService subscriptionService)
    {
        _inviteLinkRepository = inviteLinkRepository;
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _passwordValidator = passwordValidator;
        _tokenService = tokenService;
        _subscriptionService = subscriptionService;
    }

    public async Task<LoginResponse> Handle(RegisterViaInviteLinkCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = _tokenService.HashRefreshToken(request.Token);
        var link = await _inviteLinkRepository.GetByTokenHashAsync(tokenHash, cancellationToken)
            ?? throw new NotFoundException("OrgInviteLink", request.Token);

        if (!link.IsValid)
            throw new ValidationException("Token", "This invite link is no longer valid");

        var validationResult = _passwordValidator.Validate(request.Password);
        if (!validationResult.IsValid)
            throw new ValidationException("Password", string.Join("; ", validationResult.Errors));

        var existingUser = await _userRepository.GetByEmailUnfilteredAsync(request.Email, cancellationToken);
        if (existingUser != null)
            throw new ConflictException("EMAIL_EXISTS", "An account with this email already exists");

        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(
            link.OrgId,
            request.Email,
            passwordHash,
            request.DisplayName,
            link.Role,
            passwordMustChange: false);

        await _userRepository.AddAsync(user, cancellationToken);

        link.IncrementUseCount();
        await _inviteLinkRepository.UpdateAsync(link, cancellationToken);

        var accessToken = _tokenService.GenerateAccessToken(user.Id, user.OrgId, user.Role.ToString(), false, false);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _tokenService.HashRefreshToken(refreshToken);

        var refreshTokenEntity = RefreshToken.Create(
            user.Id,
            Guid.Empty,
            refreshTokenHash,
            _tokenService.GetRefreshTokenExpiration());

        await _refreshTokenRepository.AddAsync(refreshTokenEntity, cancellationToken);

        var subscriptionCheck = await _subscriptionService.CheckSubscriptionAccessAsync(link.OrgId, cancellationToken);

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = (int)_tokenService.GetAccessTokenExpiration().TotalSeconds,
            UserId = user.Id,
            OrgId = user.OrgId,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString(),
            OrgName = link.Organization?.Name ?? "",
            PasswordMustChange = false,
            SubscriptionStatus = subscriptionCheck.Status,
            PlanTier = subscriptionCheck.PlanTier ?? ""
        };
    }
}
