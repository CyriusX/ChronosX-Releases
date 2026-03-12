using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para convidar um novo usuário para a organização (B2B)
/// </summary>
public sealed record InviteUserCommand(
    Guid OrgId,
    string Email,
    string DisplayName,
    string Role) : IRequest<InviteUserResponse>;

public sealed class InviteUserCommandHandler : IRequestHandler<InviteUserCommand, InviteUserResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordGenerator _passwordGenerator;
    private readonly IEmailService _emailService;

    public InviteUserCommandHandler(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IPasswordHasher passwordHasher,
        IPasswordGenerator passwordGenerator,
        IEmailService emailService)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _passwordHasher = passwordHasher;
        _passwordGenerator = passwordGenerator;
        _emailService = emailService;
    }

    public async Task<InviteUserResponse> Handle(InviteUserCommand request, CancellationToken cancellationToken)
    {
        // Validate role
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            throw new ValidationException("Role", $"Invalid role. Valid roles are: {string.Join(", ", Enum.GetNames<UserRole>())}");
        }

        // Check if organization exists
        var organization = await _organizationRepository.GetByIdAsync(request.OrgId, cancellationToken);
        if (organization == null)
        {
            throw new NotFoundException("Organization", request.OrgId);
        }

        // Check if email already exists in the organization
        var emailExistsInOrg = await _userRepository.EmailExistsInOrgAsync(request.Email, request.OrgId, cancellationToken);
        if (emailExistsInOrg)
        {
            throw new ConflictException("EMAIL_EXISTS_IN_ORG", "An account with this email already exists in this organization");
        }

        // Check if email exists globally
        var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser != null)
        {
            throw new ConflictException("EMAIL_EXISTS", "An account with this email already exists");
        }

        // Generate temporary password
        var temporaryPassword = _passwordGenerator.GenerateTemporaryPassword();
        var passwordHash = _passwordHasher.Hash(temporaryPassword);

        // Create user
        var user = User.Create(
            request.OrgId,
            request.Email,
            passwordHash,
            request.DisplayName,
            role,
            passwordMustChange: true);

        await _userRepository.AddAsync(user, cancellationToken);

        // Send invitation email
        await _emailService.SendInvitationEmailAsync(
            request.Email,
            "An administrator",
            organization.Name,
            temporaryPassword,
            cancellationToken);

        return new InviteUserResponse
        {
            UserId = user.Id,
            Email = user.Email,
            TemporaryPassword = temporaryPassword
        };
    }
}
