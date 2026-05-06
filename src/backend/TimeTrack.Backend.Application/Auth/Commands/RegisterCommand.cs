using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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
    private const int DefaultTrialDays = 14;

    private readonly IUserRepository _userRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordValidator _passwordValidator;
    private readonly ISubscriptionPlanRepository _subscriptionPlanRepository;
    private readonly IOrgSubscriptionRepository _orgSubscriptionRepository;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IOrganizationRepository organizationRepository,
        IPasswordHasher passwordHasher,
        IPasswordValidator passwordValidator,
        ISubscriptionPlanRepository subscriptionPlanRepository,
        IOrgSubscriptionRepository orgSubscriptionRepository,
        IConfiguration configuration,
        ILogger<RegisterCommandHandler> logger)
    {
        _userRepository = userRepository;
        _organizationRepository = organizationRepository;
        _passwordHasher = passwordHasher;
        _passwordValidator = passwordValidator;
        _subscriptionPlanRepository = subscriptionPlanRepository;
        _orgSubscriptionRepository = orgSubscriptionRepository;
        _configuration = configuration;
        _logger = logger;
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

        // Grant a 14-day trial subscription so the org can use the product immediately.
        // Prefer the Free plan; fall back to Pro if Free isn't seeded (older deployments).
        try
        {
            var plan = await _subscriptionPlanRepository.GetByTierAsync(PlanTier.Free, cancellationToken)
                    ?? await _subscriptionPlanRepository.GetByTierAsync(PlanTier.Pro, cancellationToken);

            if (plan is not null)
            {
                var trialDays = _configuration.GetValue<int>("Subscription:TrialDays", DefaultTrialDays);
                if (trialDays <= 0) trialDays = DefaultTrialDays;

                var subscription = OrgSubscription.Create(organization.Id);
                subscription.EnterTrial(plan.Id, DateTime.UtcNow.AddDays(trialDays), quantity: 1);
                await _orgSubscriptionRepository.AddAsync(subscription, cancellationToken);
            }
            else
            {
                _logger.LogWarning(
                    "No SubscriptionPlan found for Free or Pro tier — org {OrgId} created without a trial subscription.",
                    organization.Id);
            }
        }
        catch (Exception ex)
        {
            // Don't fail registration if trial creation fails — users can still log in and
            // admins can recover via backfill. Log loud so ops can investigate.
            _logger.LogError(ex, "Failed to create trial subscription for org {OrgId}", organization.Id);
        }

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
