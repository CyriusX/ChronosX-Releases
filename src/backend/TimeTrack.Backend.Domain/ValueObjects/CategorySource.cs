namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Origem da classificação de categoria
///
/// SRP: Define apenas a origem da decisão de categorização
/// Importante para UX: Admin precisa saber se a classificação vem da lista global ou de override
/// </summary>
public enum CategorySource
{
    /// <summary>
    /// Classificação da lista global curada pela equipe TimeTrack
    /// </summary>
    Global = 1,

    /// <summary>
    /// Override específico da organização (feito pelo Admin)
    /// </summary>
    OrgOverride = 2,

    /// <summary>
    /// Classificação padrão para apps não mapeados (sempre Neutral/Unknown)
    /// </summary>
    Default = 3
}
