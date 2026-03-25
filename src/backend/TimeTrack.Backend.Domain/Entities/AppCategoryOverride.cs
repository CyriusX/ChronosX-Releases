using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Override de categorização específico por organização
/// Permite que Admins personalizem a classificação para sua realidade
///
/// SRP: Apenas armazena overrides por organização
/// DIP: Não depende de AppCategoryGlobal, apenas referencia por identifier
/// </summary>
public sealed class AppCategoryOverride
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public string Identifier { get; private set; } = string.Empty;
    public AppIdentifierType IdentifierType { get; private set; }
    public string? DisplayName { get; private set; }
    public AppProductivityCategory Productivity { get; private set; }
    public AppSubcategory Subcategory { get; private set; }
    public string? Note { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private AppCategoryOverride() { }

    /// <summary>
    /// Factory method para criar um override
    /// </summary>
    public static AppCategoryOverride Create(
        Guid orgId,
        string identifier,
        AppIdentifierType identifierType,
        AppProductivityCategory productivity,
        AppSubcategory subcategory,
        Guid createdBy,
        string? displayName = null,
        string? note = null)
    {
        ValidateIdentifier(identifier, identifierType);
        ValidateSubcategoryCompatibility(productivity, subcategory);
        ValidateNote(note);

        var now = DateTime.UtcNow;
        return new AppCategoryOverride
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Identifier = NormalizeIdentifier(identifier, identifierType),
            IdentifierType = identifierType,
            Productivity = productivity,
            Subcategory = subcategory,
            DisplayName = displayName?.Trim(),
            Note = note?.Trim(),
            CreatedBy = createdBy,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Atualiza um override existente
    /// </summary>
    public void Update(
        AppProductivityCategory productivity,
        AppSubcategory subcategory,
        string? displayName = null,
        string? note = null)
    {
        ValidateSubcategoryCompatibility(productivity, subcategory);
        ValidateNote(note);

        Productivity = productivity;
        Subcategory = subcategory;
        DisplayName = displayName?.Trim();
        Note = note?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    private static void ValidateIdentifier(string identifier, AppIdentifierType type)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier is required", nameof(identifier));
    }

    private static string NormalizeIdentifier(string identifier, AppIdentifierType type)
    {
        var normalized = identifier.Trim().ToLowerInvariant();

        if (type == AppIdentifierType.Exe && !normalized.EndsWith(".exe"))
        {
            normalized += ".exe";
        }

        return normalized;
    }

    private static void ValidateNote(string? note)
    {
        if (note != null && note.Length > 500)
            throw new ArgumentException("Note must be at most 500 characters", nameof(note));
    }

    private static void ValidateSubcategoryCompatibility(
        AppProductivityCategory productivity,
        AppSubcategory subcategory)
    {
        // Reuse validation logic from AppCategoryGlobal
        // This ensures consistency between global and override categories
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
        AppSubcategory.Communication => true,
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
