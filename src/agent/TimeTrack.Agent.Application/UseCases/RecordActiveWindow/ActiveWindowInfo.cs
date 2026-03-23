namespace TimeTrack.Agent.Application.UseCases.RecordActiveWindow;

/// <summary>
/// Request para registrar janela ativa
/// </summary>
public sealed record RecordActiveWindowRequest
{
    /// <summary>
    /// Caminho do executável da aplicação
    /// </summary>
    public required string ExecutablePath { get; init; }

    /// <summary>
    /// Nome da aplicação
    /// </summary>
    public required string ApplicationName { get; init; }

    /// <summary>
    /// Título da janela
    /// </summary>
    public string? WindowTitle { get; init; }

    /// <summary>
    /// Caminho do arquivo ou pasta ativo (quando disponível)
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// URL do browser (quando a janela ativa é um navegador).
    /// Ex: "https://github.com/my-repo/issues"
    /// </summary>
    public string? BrowserUrl { get; init; }

    /// <summary>
    /// Momento da captura (UTC)
    /// </summary>
    public DateTime CapturedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Response do registro de janela ativa
/// </summary>
public sealed record RecordActiveWindowResponse
{
    /// <summary>
    /// ID da sessão criada/atualizada
    /// </summary>
    public Guid SessionId { get; init; }

    /// <summary>
    /// Nome da aplicação
    /// </summary>
    public required string ApplicationName { get; init; }

    /// <summary>
    /// Indica se foi uma nova sessão ou extensão de existente
    /// </summary>
    public bool IsNewSession { get; init; }

    /// <summary>
    /// Duração atual da sessão
    /// </summary>
    public TimeSpan SessionDuration { get; init; }
}
