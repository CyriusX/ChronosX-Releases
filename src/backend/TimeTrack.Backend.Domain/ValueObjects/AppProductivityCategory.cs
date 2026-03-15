namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Categoria de produtividade de uma aplicação
/// Usada para calcular o Focus Score baseado no tipo de app utilizado
///
/// SRP: Define apenas a classificação de produtividade
/// </summary>
public enum AppProductivityCategory
{
    /// <summary>
    /// Aplicação produtiva - contribui positivamente para o foco
    /// Exemplos: VS Code, Figma, Word, Excel, Terminal
    /// </summary>
    Productive = 1,

    /// <summary>
    /// Aplicação neutra - não afeta o foco significativamente
    /// Exemplos: Chrome, Firefox (depende do contexto/título)
    /// </summary>
    Neutral = 2,

    /// <summary>
    /// Aplicação de distração - reduz o foco
    /// Exemplos: Spotify, YouTube, Steam, Instagram, WhatsApp
    /// </summary>
    Distraction = 3
}
