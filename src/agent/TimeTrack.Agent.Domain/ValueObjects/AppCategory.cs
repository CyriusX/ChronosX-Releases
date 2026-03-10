namespace TimeTrack.Agent.Domain.ValueObjects;

/// <summary>
/// Representa a categoria de produtividade de uma aplicação
/// </summary>
public sealed record AppCategory
{
    /// <summary>
    /// Nível de produtividade: "productive" | "neutral" | "distraction"
    /// </summary>
    public string Productivity { get; init; }

    /// <summary>
    /// Subcategoria: ex: "development", "social_media", "unknown"
    /// </summary>
    public string Subcategory { get; init; }

    /// <summary>
    /// Origem da classificação: "org_override" | "global" | "default"
    /// </summary>
    public string Source { get; init; }

    private AppCategory()
    {
        Productivity = "neutral";
        Subcategory = "unknown";
        Source = "default";
    }

    public AppCategory(string productivity, string subcategory, string source)
    {
        Productivity = productivity ?? throw new ArgumentNullException(nameof(productivity));
        Subcategory = subcategory ?? throw new ArgumentNullException(nameof(subcategory));
        Source = source ?? throw new ArgumentNullException(nameof(source));
    }

    /// <summary>
    /// Categoria padrão para apps não classificados
    /// </summary>
    public static AppCategory Unknown => new()
    {
        Productivity = "neutral",
        Subcategory = "unknown",
        Source = "default"
    };

    /// <summary>
    /// Cria uma categoria produtiva
    /// </summary>
    public static AppCategory Productive(string subcategory, string source = "global")
        => new("productive", subcategory, source);

    /// <summary>
    /// Cria uma categoria de distração
    /// </summary>
    public static AppCategory Distraction(string subcategory, string source = "global")
        => new("distraction", subcategory, source);

    /// <summary>
    /// Cria uma categoria neutra
    /// </summary>
    public static AppCategory Neutral(string subcategory, string source = "global")
        => new("neutral", subcategory, source);

    public bool IsProductive => Productivity == "productive";
    public bool IsDistraction => Productivity == "distraction";
    public bool IsNeutral => Productivity == "neutral";
}
 