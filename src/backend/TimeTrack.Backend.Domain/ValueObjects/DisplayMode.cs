namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Modo de exibição do Agent
/// </summary>
public enum DisplayMode
{
    /// <summary>
    /// Roda em background (system tray)
    /// </summary>
    Background = 1,

    /// <summary>
    /// Mostra UI visível (janela)
    /// </summary>
    Foreground = 2
}
