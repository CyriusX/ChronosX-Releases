namespace TimeTrack.Agent.Application.UseCases.GetLocalDashboard;

/// <summary>
/// Response do dashboard local do Agent
/// </summary>
public sealed record LocalDashboardResponse
{
    /// <summary>
    /// Data de referência do dashboard
    /// </summary>
    public DateTime Date { get; init; }

    /// <summary>
    /// Status atual do tracking
    /// </summary>
    public required string TrackingStatus { get; init; }

    /// <summary>
    /// Tempo total trabalhado no dia
    /// </summary>
    public TimeSpan TotalWorkTime { get; init; }

    /// <summary>
    /// Tempo total de inatividade no dia
    /// </summary>
    public TimeSpan TotalIdleTime { get; init; }

    /// <summary>
    /// Tempo em apps produtivos (milissegundos)
    /// </summary>
    public long FocusTimeMs { get; init; }

    /// <summary>
    /// Score de foco calculado (0-100)
    /// </summary>
    public short FocusScore { get; init; }

    /// <summary>
    /// Número de sessões registradas
    /// </summary>
    public int SessionCount { get; init; }

    /// <summary>
    /// Top aplicações mais usadas
    /// </summary>
    public IReadOnlyList<AppUsageSummary> TopApplications { get; init; } = Array.Empty<AppUsageSummary>();

    /// <summary>
    /// Última sessão de atividade
    /// </summary>
    public ActivitySessionSummary? LastSession { get; init; }
}

/// <summary>
/// Resumo de uso de uma aplicação
/// </summary>
public sealed record AppUsageSummary
{
    /// <summary>
    /// Nome da aplicação
    /// </summary>
    public required string DisplayName { get; init; }

    /// </summary>
    /// Tempo total de uso
    /// </summary>
    public TimeSpan TotalTime { get; init; }

    /// <summary>
    /// Percentual do tempo total
    /// </summary>
    public double Percentage { get; init; }

    /// <summary>
    /// Categoria de produtividade
    /// </summary>
    public required string ProductivityCategory { get; init; }
}

/// <summary>
/// Resumo de uma sessão de atividade
/// </summary>
public sealed record ActivitySessionSummary
{
    /// <summary>
    /// Nome da aplicação
    /// </summary>
    public required string AppName { get; init; }

    /// <summary>
    /// Título da janela
    /// </summary>
    public string? WindowTitle { get; init; }

    /// <summary>
    /// Início da sessão
    /// </summary>
    public DateTime StartUtc { get; init; }

    /// <summary>
    /// Fim da sessão
    /// </summary>
    public DateTime EndUtc { get; init; }

    /// <summary>
    /// Duração da sessão
    /// </summary>
    public TimeSpan Duration { get; init; }
}
