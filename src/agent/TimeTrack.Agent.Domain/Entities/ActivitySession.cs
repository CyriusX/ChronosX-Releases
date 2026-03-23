using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Domain.Entities;

/// <summary>
/// Representa uma sessão de atividade do usuário em uma aplicação
/// </summary>
public sealed class ActivitySession : EntityBase
{
    /// <summary>
    /// ID do usuário proprietário desta sessão
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Identidade da aplicação
    /// </summary>
    public AppIdentity App { get; }

    /// <summary>
    /// Intervalo de tempo da sessão
    /// </summary>
    public TimeRange Period { get; private set; }

    /// <summary>
    /// Hash da janela ativa (para agrupamento de sessões similares)
    /// </summary>
    public string? WindowHash { get; }

    /// <summary>
    /// Título da janela no momento da sessão
    /// </summary>
    public string? WindowTitle { get; }

    /// <summary>
    /// Caminho do arquivo ou pasta ativo (quando disponível)
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Domínio/URL do site quando a sessão é de um navegador.
    /// Ex: "github.com", "web.telegram.org"
    /// </summary>
    public string? Domain { get; }

    private ActivitySession() { }

    public ActivitySession(
        Guid id,
        Guid userId,
        AppIdentity app,
        TimeRange period,
        string? windowHash = null,
        string? windowTitle = null,
        string? filePath = null,
        string? domain = null)
        : base(id)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required", nameof(userId));

        UserId = userId;
        App = app ?? throw new ArgumentNullException(nameof(app));
        Period = period ?? throw new ArgumentNullException(nameof(period));
        WindowHash = windowHash;
        WindowTitle = windowTitle;
        FilePath = filePath;
        Domain = domain;
    }

    /// <summary>
    /// Cria uma nova sessão com ID gerado automaticamente
    /// </summary>
    public static ActivitySession Create(
        Guid userId,
        AppIdentity app,
        TimeRange period,
        string? windowHash = null,
        string? windowTitle = null,
        string? filePath = null,
        string? domain = null)
    {
        return new ActivitySession(Guid.NewGuid(), userId, app, period, windowHash, windowTitle, filePath, domain);
    }

    /// <summary>
    /// Estende o período da sessão até o novo momento de término
    /// </summary>
    public void Extend(DateTime newEndUtc)
    {
        if (newEndUtc <= Period.StartUtc)
            throw DomainException.InvalidTimeRange();

        Period = new TimeRange(Period.StartUtc, newEndUtc);
    }

    /// <summary>
    /// Estende o período com base no tempo adicional (duração em segundos)
    /// </summary>
    /// <param name="additionalSeconds">Segundos to adicionar ao end time</param>
    public void Extend(int additionalSeconds)
    {
        if (additionalSeconds <= 0)
            throw new ArgumentException("Additional seconds must be positive", nameof(additionalSeconds));

        Period = new TimeRange(Period.StartUtc, Period.EndUtc.AddSeconds(additionalSeconds));
    }

    /// <summary>
    /// Duração da sessão
    /// </summary>
    public TimeSpan Duration => Period.Duration;

    public override string ToString()
        => $"Session[{Id}] {App.DisplayName} {Period}";
}
