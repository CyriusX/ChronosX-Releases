namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

/// <summary>
/// Interface para consultas de relatórios otimizadas
///
/// SRP: Apenas consultas de agregação para relatórios
/// OCP: Novos métodos podem ser adicionados sem modificar existentes
/// </summary>
public interface IReportRepository
{
    /// <summary>
    /// Obtém totais de atividade agregados por app para um dia específico
    /// </summary>
    Task<DailyActivityAggregate> GetDailyActivityAggregateAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém total de segundos de idle para um dia específico
    /// </summary>
    Task<long> GetDailyIdleSecondsAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém top apps por período ordenados por tempo total
    /// </summary>
    Task<IEnumerable<AppAggregate>> GetTopAppsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        int limit,
        string? productivityFilter,
        CancellationToken cancellationToken = default);

    // ========================================================================
    // NOVOS MÉTODOS PARA CX-155 - Página de Relatório
    // ========================================================================

    /// <summary>
    /// Obtém resumo diário para múltiplos dias (heatmap estilo GitHub)
    /// </summary>
    Task<IEnumerable<DailySummaryItem>> GetDailySummaryRangeAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém tendência de produtividade por período (barras empilhadas)
    /// </summary>
    Task<IEnumerable<ProductivityTrendItem>> GetProductivityTrendAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        string groupBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém top URLs e caminhos extraídos de window_title
    /// </summary>
    Task<IEnumerable<TopPathItem>> GetTopPathsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém estatísticas de distração (top 5 apps + linha do tempo)
    /// </summary>
    Task<DistractionStats> GetDistractionStatsAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém distribuição por categoria de produtividade
    /// </summary>
    Task<IEnumerable<CategoryDistributionItem>> GetCategoryDistributionAsync(
        Guid userId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Agregado de atividade diária
/// </summary>
public sealed class DailyActivityAggregate
{
    public long TotalSeconds { get; init; }
    public DateTime? FirstActivity { get; init; }
    public DateTime? LastActivity { get; init; }
    public List<AppAggregate> Apps { get; init; } = [];
}

/// <summary>
/// Agregado por app com categoria de produtividade
/// </summary>
public sealed class AppAggregate
{
    public string ProcessName { get; init; } = string.Empty;
    public long TotalSeconds { get; init; }
    public int SessionCount { get; init; }
    /// <summary>
    /// Categoria de produtividade: productive, neutral, distraction
    /// </summary>
    public string? Productivity { get; init; }
    /// <summary>
    /// Subcategoria: development, social_media, etc.
    /// </summary>
    public string? Subcategory { get; init; }
    /// <summary>
    /// Nome de exibição amigável
    /// </summary>
    public string? DisplayName { get; init; }
}

// ============================================================================
// DTOs PARA NOVOS ENDPOINTS - CX-155
// ============================================================================

/// <summary>
/// Item de resumo diário para heatmap
/// </summary>
public sealed class DailySummaryItem
{
    public DateTime Date { get; init; }
    public long TotalActiveSeconds { get; init; }
    public long TotalIdleSeconds { get; init; }
    /// <summary>
    /// Razão de produtividade (0.0 a 1.0)
    /// </summary>
    public double ProductivityRatio { get; init; }
}

/// <summary>
/// Item de tendência de produtividade por período
/// </summary>
public sealed class ProductivityTrendItem
{
    /// <summary>
    /// Período: data (dia), semana, ou mês
    /// </summary>
    public string Period { get; init; } = string.Empty;
    public long ProductiveSeconds { get; init; }
    public long NeutralSeconds { get; init; }
    public long DistractionSeconds { get; init; }
    public long IdleSeconds { get; init; }
}

/// <summary>
/// Item de URL ou caminho mais acessado
/// </summary>
public sealed class TopPathItem
{
    /// <summary>
    /// Título principal (nome do projeto, site, ou aplicação)
    /// </summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>
    /// Caminho do arquivo ou URL extraído
    /// </summary>
    public string? FilePath { get; init; }
    /// <summary>
    /// URL ou caminho completo (legado, para compatibilidade)
    /// </summary>
    public string Path { get; init; } = string.Empty;
    /// <summary>
    /// App de origem (chrome.exe, code.exe, explorer.exe)
    /// </summary>
    public string SourceApp { get; init; } = string.Empty;
    public long TotalSeconds { get; init; }
    public int VisitCount { get; init; }
}

/// <summary>
/// Estatísticas de distração
/// </summary>
public sealed class DistractionStats
{
    /// <summary>
    /// Tempo improdutivo por dia no período
    /// </summary>
    public List<DailyDistraction> DailyDistractions { get; init; } = [];
    /// <summary>
    /// Top 5 apps de distração
    /// </summary>
    public List<AppAggregate> TopDistractions { get; init; } = [];
}

/// <summary>
/// Distração diária
/// </summary>
public sealed class DailyDistraction
{
    public DateTime Date { get; init; }
    public long DistractionSeconds { get; init; }
}

/// <summary>
/// Item de distribuição por categoria
/// </summary>
public sealed class CategoryDistributionItem
{
    /// <summary>
    /// Categoria principal: productive, neutral, distraction
    /// </summary>
    public string Category { get; init; } = string.Empty;
    public long TotalSeconds { get; init; }
    public double Percentage { get; init; }
    /// <summary>
    /// Subcategorias detalhadas
    /// </summary>
    public List<SubcategoryItem> Subcategories { get; init; } = [];
}

/// <summary>
/// Subcategoria dentro de uma categoria
/// </summary>
public sealed class SubcategoryItem
{
    public string Name { get; init; } = string.Empty;
    public long TotalSeconds { get; init; }
    public double Percentage { get; init; }
}
