namespace TimeTrack.Agent.Domain.ValueObjects;

/// <summary>
/// Tipo de identificador de aplicativo
/// </summary>
public enum AppIdentifierType
{
    Exe = 1,
    Domain = 2
}

/// <summary>
/// Categoria de produtividade
/// </summary>
public enum AppProductivityCategory
{
    Productive = 1,
    Neutral = 2,
    Distraction = 3
}

/// <summary>
/// Origem da classificação
/// </summary>
public enum CategorySource
{
    Global = 1,
    OrgOverride = 2,
    Default = 3
}
