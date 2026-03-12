using MediatR;
using TimeTrack.Backend.Application.Auth.DTOs;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Auth.Commands;

/// <summary>
/// Command para registro de nova organização e usuário admin (B2C)
/// </summary>
public sealed record RegisterCommand(
    string Email,
    string Password,
    string DisplayName,
    string OrganizationName) : IRequest<RegisterResponse>;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, RegisterResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordValidator _passwordValidator;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IPasswordHasher passwordHasher,
        IPasswordValidator passwordValidator)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _passwordHasher = passwordHasher;
        _passwordValidator = passwordValidator;
    }

    public async Task<RegisterResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // Validate password complexity
        var validationResult = _passwordValidator.Validate(request.Password);
        if (!validationResult.IsValid)
        {
            throw new ValidationException("Password", string.Join("; ", validationResult.Errors));
        }

        // Check if email already exists
        var existingUser = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser != null)
        {
            throw new ConflictException("EMAIL_EXISTS", "An account with this email already exists");
        }

        // Generate unique slug from organization name
        var baseSlug = GenerateSlug(request.OrganizationName);
        var slug = baseSlug;
        var counter = 1;

        while (await _organizationRepository.SlugExistsAsync(slug, cancellationToken))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }

        // Create organization
        var organization = Organization.Create(request.OrganizationName, slug, OrgType.Business);
        await _organizationRepository.AddAsync(organization, cancellationToken);

        // Create user as admin of the organization
        var passwordHash = _passwordHasher.Hash(request.Password);
        var user = User.Create(
            organization.Id,
            request.Email,
            passwordHash,
            request.DisplayName,
            UserRole.Admin,
            passwordMustChange: false);

        await _userRepository.AddAsync(user, cancellationToken);

        return new RegisterResponse
        {
            UserId = user.Id,
            OrgId = organization.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            OrganizationName = organization.Name
        };
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant();
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9]+", "-");
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');
        return slug.Length > 50 ? slug[..50] : slug;
    }
}
