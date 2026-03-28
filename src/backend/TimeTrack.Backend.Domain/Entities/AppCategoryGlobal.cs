using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Lista global de categorização de aplicativos e sites
/// Mantida pela equipe TimeTrack, nunca editada por clientes
///
/// SRP: Apenas armazena a classificação global
/// OCP: Novas categorias podem ser adicionadas via seed
/// </summary>
public sealed class AppCategoryGlobal
{
    public Guid Id { get; private set; }
    public string Identifier { get; private set; } = string.Empty;
    public AppIdentifierType IdentifierType { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public AppProductivityCategory Productivity { get; private set; }
    public AppSubcategory Subcategory { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Parameterless constructor for EF Core
    private AppCategoryGlobal() { }

    /// <summary>
    /// Factory method para criar uma categoria global
    /// </summary>
    public static AppCategoryGlobal Create(
        string identifier,
        AppIdentifierType identifierType,
        string displayName,
        AppProductivityCategory productivity,
        AppSubcategory subcategory)
    {
        ValidateIdentifier(identifier, identifierType);
        ValidateDisplayName(displayName);
        ValidateSubcategoryCompatibility(productivity, subcategory);

        var now = DateTime.UtcNow;
        return new AppCategoryGlobal
        {
            Id = Guid.NewGuid(),
            Identifier = IdentifierNormalizer.Normalize(identifier, identifierType),
            IdentifierType = identifierType,
            DisplayName = displayName.Trim(),
            Productivity = productivity,
            Subcategory = subcategory,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Atualiza a categoria global (apenas para manutenção interna)
    /// </summary>
    public void Update(
        string displayName,
        AppProductivityCategory productivity,
        AppSubcategory subcategory)
    {
        ValidateDisplayName(displayName);
        ValidateSubcategoryCompatibility(productivity, subcategory);

        DisplayName = displayName.Trim();
        Productivity = productivity;
        Subcategory = subcategory;
        UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateIdentifier(string identifier, AppIdentifierType type)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier is required", nameof(identifier));

        var normalized = identifier.Trim().ToLowerInvariant();

        if (type == AppIdentifierType.Domain)
        {
            if (!IsValidDomain(normalized))
                throw new ArgumentException($"Invalid domain format: {identifier}");
        }
        else if (type == AppIdentifierType.Exe)
        {
            if (!normalized.EndsWith(".exe") && !IsValidExeName(normalized))
                throw new ArgumentException($"Invalid exe name format: {identifier}");
        }
    }

    private static bool IsValidDomain(string domain)
    {
        // Basic domain validation
        return domain.Contains('.') && !domain.Contains(' ') && domain.Length >= 3;
    }

    private static bool IsValidExeName(string name)
    {
        // Allow names with or without .exe extension
        return !string.IsNullOrWhiteSpace(name) && !name.Contains('/');
    }

    private static void ValidateDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required", nameof(displayName));

        if (displayName.Length > 100)
            throw new ArgumentException("Display name must be at most 100 characters", nameof(displayName));
    }

    private static void ValidateSubcategoryCompatibility(
        AppProductivityCategory productivity,
        AppSubcategory subcategory)
    {
        var isValid = productivity switch
        {
            AppProductivityCategory.Productive => IsProductiveSubcategory(subcategory),
            AppProductivityCategory.Neutral => IsNeutralSubcategory(subcategory),
            AppProductivityCategory.Distraction => IsDistractionSubcategory(subcategory),
            _ => false
        };

        if (!isValid)
            throw new ArgumentException(
                $"Subcategory {subcategory} is not compatible with productivity {productivity}");
    }

    private static bool IsProductiveSubcategory(AppSubcategory sub) => sub switch
    {
        AppSubcategory.Development => true,
        AppSubcategory.Design => true,
        AppSubcategory.Communication => true,
        AppSubcategory.ProductivityTools => true,
        AppSubcategory.Productivity => true,
        AppSubcategory.Meetings => true,
        AppSubcategory.Documentation => true,
        AppSubcategory.DevOps => true,
        AppSubcategory.Finance => true,
        _ => false
    };

    private static bool IsNeutralSubcategory(AppSubcategory sub) => sub switch
    {
        AppSubcategory.BrowserGeneral => true,
        AppSubcategory.System => true,
        AppSubcategory.Unknown => true,
        AppSubcategory.FileManager => true,
        AppSubcategory.Utilities => true,
        AppSubcategory.Communication => true, // WhatsApp, Telegram, etc.
        _ => false
    };

    private static bool IsDistractionSubcategory(AppSubcategory sub) => sub switch
    {
        AppSubcategory.SocialMedia => true,
        AppSubcategory.Entertainment => true,
        AppSubcategory.Gaming => true,
        AppSubcategory.News => true,
        AppSubcategory.MusicStreaming => true,
        AppSubcategory.Shopping => true,
        _ => false
    };
}
