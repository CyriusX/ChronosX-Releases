using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using MediatR;

namespace TimeTrack.Backend.Application.Reports.DTOs;

// ============================================================================
// DAILY SUMMARY RANGE - Heatmap estilo GitHub
// ============================================================================

/// <summary>
/// Query para obter resumo diário de um período (heatmap)
/// </summary>
public sealed record DailySummaryRangeQuery(
    [Required] Guid? UserId,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    string? Timezone = null
) : IRequest<DailySummaryRangeResponse>;

/// <summary>
/// Response do resumo diário por período
/// </summary>
public sealed class DailySummaryRangeResponse
{
    /// <summary>
    /// Lista de resumos diários
    /// </summary>
    [JsonPropertyName("days")]
    public List<DailySummaryDayItem> Days { get; init; } = [];

    /// <summary>
    /// Focus Score agregado do período (0-100)
    /// Calculado com base no tempo produtivo total, penalidades por distração e bônus por blocos de foco
    /// </summary>
    [JsonPropertyName("periodFocusScore")]
    public short PeriodFocusScore { get; init; }

    /// <summary>
    /// Proporção base de produtividade do período (0.0 a 1.0)
    /// Simples: tempo_produtivo / tempo_total
    /// </summary>
    [JsonPropertyName("periodBaseProductivity")]
    public double PeriodBaseProductivity { get; init; }
}

/// <summary>
/// Item de resumo de um dia
/// </summary>
public sealed class DailySummaryDayItem
{
    /// <summary>
    /// Data no formato YYYY-MM-DD
    /// </summary>
    [JsonPropertyName("date")]
    public string Date { get; init; } = string.Empty;

    /// <summary>
    /// Total de segundos ativos no dia
    /// </summary>
    [JsonPropertyName("totalActiveSeconds")]
    public long TotalActiveSeconds { get; init; }

    /// <summary>
    /// Total de segundos idle no dia
    /// </summary>
    [JsonPropertyName("totalIdleSeconds")]
    public long TotalIdleSeconds { get; init; }

    /// <summary>
    /// Razão de produtividade (0.0 a 1.0)
    /// </summary>
    [JsonPropertyName("productivityRatio")]
    public double ProductivityRatio { get; init; }

    /// <summary>
    /// Focus Score do dia (0-100)
    /// Inclui penalidades por distração e bônus por blocos de foco longo
    /// </summary>
    [JsonPropertyName("focusScore")]
    public short FocusScore { get; init; }
}

// ============================================================================
// PRODUCTIVITY TREND - Gráfico de barras empilhadas
// ============================================================================

/// <summary>
/// Query para obter tendência de produtividade
/// </summary>
public sealed record ProductivityTrendQuery(
    [Required] Guid? UserId,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    string GroupBy = "day",
    string? Timezone = null
) : IRequest<ProductivityTrendResponse>;

/// <summary>
/// Response da tendência de produtividade
/// </summary>
public sealed class ProductivityTrendResponse
{
    /// <summary>
    /// Lista de períodos com dados de produtividade
    /// </summary>
    [JsonPropertyName("periods")]
    public List<ProductivityTrendPeriodItem> Periods { get; init; } = [];
}

/// <summary>
/// Item de tendência por período
/// </summary>
public sealed class ProductivityTrendPeriodItem
{
    /// <summary>
    /// Identificador do período (YYYY-MM-DD, YYYY-WXX, YYYY-MM)
    /// </summary>
    [JsonPropertyName("period")]
    public string Period { get; init; } = string.Empty;

    /// <summary>
    /// Segundos de atividade produtiva
    /// </summary>
    [JsonPropertyName("productiveSeconds")]
    public long ProductiveSeconds { get; init; }

    /// <summary>
    /// Segundos de atividade neutra
    /// </summary>
    [JsonPropertyName("neutralSeconds")]
    public long NeutralSeconds { get; init; }

    /// <summary>
    /// Segundos de atividade de distração
    /// </summary>
    [JsonPropertyName("distractionSeconds")]
    public long DistractionSeconds { get; init; }

    /// <summary>
    /// Segundos de idle
    /// </summary>
    [JsonPropertyName("idleSeconds")]
    public long IdleSeconds { get; init; }
}

// ============================================================================
// TOP PATHS - URLs e caminhos mais acessados
// ============================================================================

/// <summary>
/// Query para obter top URLs e caminhos
/// </summary>
public sealed record TopPathsQuery(
    [Required] Guid? UserId,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    int Limit = 20,
    string? Timezone = null
) : IRequest<TopPathsResponse>;

/// <summary>
/// Response dos top paths
/// </summary>
public sealed class TopPathsResponse
{
    /// <summary>
    /// Lista de URLs e caminhos mais acessados
    /// </summary>
    [JsonPropertyName("paths")]
    public List<TopPathResponseItem> Paths { get; init; } = [];
}

/// <summary>
/// Item de URL ou caminho
/// </summary>
public sealed class TopPathResponseItem
{
    /// <summary>
    /// Título principal (nome do projeto, site, ou aplicação)
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Caminho do arquivo ou URL extraído
    /// </summary>
    [JsonPropertyName("filePath")]
    public string? FilePath { get; init; }

    /// <summary>
    /// URL ou caminho completo (legado, para compatibilidade)
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// App de origem (chrome.exe, code.exe, etc.)
    /// </summary>
    [JsonPropertyName("sourceApp")]
    public string SourceApp { get; init; } = string.Empty;

    /// <summary>
    /// Total de segundos
    /// </summary>
    [JsonPropertyName("totalSeconds")]
    public long TotalSeconds { get; init; }

    /// <summary>
    /// Número de visitas/sessões
    /// </summary>
    [JsonPropertyName("visitCount")]
    public int VisitCount { get; init; }
}

// ============================================================================
// DISTRACTION STATS - Estatísticas de distração
// ============================================================================

/// <summary>
/// Query para obter estatísticas de distração
/// </summary>
public sealed record DistractionStatsQuery(
    [Required] Guid? UserId,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    string? Timezone = null
) : IRequest<DistractionStatsResponse>;

/// <summary>
/// Response das estatísticas de distração
/// </summary>
public sealed class DistractionStatsResponse
{
    /// <summary>
    /// Distração por dia no período
    /// </summary>
    [JsonPropertyName("dailyDistractions")]
    public List<DailyDistractionItem> DailyDistractions { get; init; } = [];

    /// <summary>
    /// Top 5 apps de distração
    /// </summary>
    [JsonPropertyName("topDistractions")]
    public List<TopDistractionItem> TopDistractions { get; init; } = [];
}

/// <summary>
/// Distração de um dia específico
/// </summary>
public sealed class DailyDistractionItem
{
    /// <summary>
    /// Data no formato YYYY-MM-DD
    /// </summary>
    [JsonPropertyName("date")]
    public string Date { get; init; } = string.Empty;

    /// <summary>
    /// Total de segundos de distração
    /// </summary>
    [JsonPropertyName("distractionSeconds")]
    public long DistractionSeconds { get; init; }
}

/// <summary>
/// Item de app de distração
/// </summary>
public sealed class TopDistractionItem
{
    /// <summary>
    /// Nome de exibição do app
    /// </summary>
    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Nome do processo
    /// </summary>
    [JsonPropertyName("processName")]
    public string ProcessName { get; init; } = string.Empty;

    /// <summary>
    /// Total de segundos
    /// </summary>
    [JsonPropertyName("totalSeconds")]
    public long TotalSeconds { get; init; }

    /// <summary>
    /// Número de sessões
    /// </summary>
    [JsonPropertyName("sessionCount")]
    public int SessionCount { get; init; }

    /// <summary>
    /// Subcategoria (entertainment, social_media, etc.)
    /// </summary>
    [JsonPropertyName("subcategory")]
    public string? Subcategory { get; init; }
}

// ============================================================================
// CATEGORY DISTRIBUTION - Distribuição por categoria
// ============================================================================

/// <summary>
/// Query para obter distribuição por categoria
/// </summary>
public sealed record CategoryDistributionQuery(
    [Required] Guid? UserId,
    [Required] DateTime StartDate,
    [Required] DateTime EndDate,
    string? Timezone = null
) : IRequest<CategoryDistributionResponse>;

/// <summary>
/// Response da distribuição por categoria
/// </summary>
public sealed class CategoryDistributionResponse
{
    /// <summary>
    /// Lista de categorias com distribuição
    /// </summary>
    [JsonPropertyName("categories")]
    public List<CategoryDistributionResponseItem> Categories { get; init; } = [];
}

/// <summary>
/// Item de categoria
/// </summary>
public sealed class CategoryDistributionResponseItem
{
    /// <summary>
    /// Nome da categoria (productive, neutral, distraction)
    /// </summary>
    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Total de segundos
    /// </summary>
    [JsonPropertyName("totalSeconds")]
    public long TotalSeconds { get; init; }

    /// <summary>
    /// Percentual do total
    /// </summary>
    [JsonPropertyName("percentage")]
    public double Percentage { get; init; }

    /// <summary>
    /// Subcategorias
    /// </summary>
    [JsonPropertyName("subcategories")]
    public List<SubcategoryResponseItem> Subcategories { get; init; } = [];
}

/// <summary>
/// Item de subcategoria
/// </summary>
public sealed class SubcategoryResponseItem
{
    /// <summary>
    /// Nome da subcategoria
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Total de segundos
    /// </summary>
    [JsonPropertyName("totalSeconds")]
    public long TotalSeconds { get; init; }

    /// <summary>
    /// Percentual dentro da categoria
    /// </summary>
    [JsonPropertyName("percentage")]
    public double Percentage { get; init; }
}
