using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MediatR;

namespace TimeTrack.Backend.Application.Reports.DTOs;

/// <summary>
/// Query para obter resumo diário de atividade
/// </summary>
public sealed record DailySummaryQuery(
    [Required]
    Guid? UserId,

    [Required]
    DateTime Date,

    string? Timezone = null
) : IRequest<DailySummaryResponse>;

/// <summary>
/// Response do resumo diário
/// </summary>
public sealed class DailySummaryResponse
{
    /// <summary>
    /// Data do resumo (formato YYYY-MM-DD)
    /// </summary>
    [JsonPropertyName("date")]
    public string Date { get; init; } = string.Empty;

    /// <summary>
    /// Total de segundos ativos no dia
    /// </summary>
    [JsonPropertyName("totalActiveSeconds")]
    public long TotalActiveSeconds { get; init; }

    /// <summary>
    /// Total de segundos de inatividade no dia
    /// </summary>
    [JsonPropertyName("totalIdleSeconds")]
    public long TotalIdleSeconds { get; init; }

    /// <summary>
    /// Horário da primeira atividade do dia (formato HH:MM:SS)
    /// </summary>
    [JsonPropertyName("firstActivity")]
    public string? FirstActivity { get; init; }

    /// <summary>
    /// Horário da última atividade do dia (formato HH:MM:SS)
    /// </summary>
    [JsonPropertyName("lastActivity")]
    public string? LastActivity { get; init; }

    /// <summary>
    /// Lista de apps usados no dia
    /// </summary>
    [JsonPropertyName("apps")]
    public List<DailyAppSummary> Apps { get; init; } = [];
}

/// <summary>
/// Resumo de um app no dia
/// </summary>
public sealed class DailyAppSummary
{
    /// <summary>
    /// Raw process name (used for internal-app filtering on the agent side)
    /// </summary>
    [JsonPropertyName("processName")]
    public string ProcessName { get; init; } = string.Empty;

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
    /// Categoria do app (ex: "development", "productivity_tools", "entertainment")
    /// </summary>
    [JsonPropertyName("appCategory")]
    public string? AppCategory { get; init; }
}
