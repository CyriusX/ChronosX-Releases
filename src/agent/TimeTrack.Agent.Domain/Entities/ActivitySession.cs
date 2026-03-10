using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Domain.Entities;

/// <summary>
/// Representa uma sessão de atividade do usuário em uma aplicação
/// </summary>
public sealed class ActivitySession : EntityBase
{
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

    private ActivitySession() { }

    public ActivitySession(
        Guid id,
        AppIdentity app,
        TimeRange period,
        string? windowHash = null,
        string? windowTitle = null)
        : base(id)
    {
        App = app ?? throw new ArgumentNullException(nameof(app));
        Period = period ?? throw new ArgumentNullException(nameof(period));
        WindowHash = windowHash;
        WindowTitle = windowTitle;
    }

    /// <summary>
    /// Cria uma nova sessão com ID gerado automaticamente
    /// </summary>
    public static ActivitySession Create(
        AppIdentity app,
        TimeRange period,
        string? windowHash = null,
        string? windowTitle = null)
    {
        return new ActivitySession(Guid.NewGuid(), app, period, windowHash, windowTitle);
    }

    /// <summary>
    /// Estende o período da sessão até um novo momento de término
    /// </summary>
    public void Extend(DateTime newEndUtc)
    {
        if (newEndUtc <= Period.StartUtc)
            throw DomainException.InvalidTimeRange();

        Period = new TimeRange(Period.StartUtc, newEndUtc);
    }

    /// <summary>
    /// Duração da sessão
    /// </summary>
    public TimeSpan Duration => Period.Duration;

    public override string ToString()
        => $"Session[{Id}] {App.DisplayName} {Period}";
}
