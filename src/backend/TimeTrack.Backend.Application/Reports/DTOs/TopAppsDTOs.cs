using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MediatR;

namespace TimeTrack.Backend.Application.Reports.DTOs;

/// <summary>
/// Query para obter top apps por período
/// </summary>
public sealed record TopAppsQuery(
    [Required]
    Guid? UserId,

    [Required]
    DateTime StartDate,

    [Required]
    DateTime EndDate,

    int Limit = 10,
    string? Timezone = null,
    IReadOnlyList<Guid>? UserIds = null
) : IRequest<TopAppsResponse>;

/// <summary>
/// Response dos top apps
/// </summary>
public sealed class TopAppsResponse
{
    /// <summary>
    /// Lista de apps ordenados por tempo total (desc)
    /// </summary>
    [JsonPropertyName("apps")]
    public List<TopAppItem> Apps { get; init; } = [];
}

/// <summary>
/// Item de app no ranking
/// </summary>
public sealed class TopAppItem
{
    /// <summary>
    /// Nome de exibição do app
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Total de segundos usando o app
    /// </summary>
    [JsonPropertyName("totalSeconds")]
    public long TotalSeconds { get; init; }

    /// <summary>
    /// Número de sessões do app
    /// </summary>
    [JsonPropertyName("sessionCount")]
    public int SessionCount { get; init; }

    /// <summary>
    /// Categoria de produtividade: productive, neutral, distraction
    /// </summary>
    [JsonPropertyName("productivity")]
    public string? Productivity { get; init; }

    /// <summary>
    /// Subcategoria: development, social_media, etc.
    /// </summary>
    [JsonPropertyName("subcategory")]
    public string? Subcategory { get; init; }

    /// <summary>
    /// Percentual do tempo total
    /// </summary>
    [JsonPropertyName("percentage")]
    public double Percentage { get; init; }
}
