using TimeTrack.Agent.Domain.Common;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Domain.Entities;

/// <summary>
/// Identidade única de uma aplicação executada no sistema
/// </summary>
public sealed class AppIdentity : EntityBase
{
    /// <summary>
    /// Hash do caminho do executável (SHA256 truncado)
    /// </summary>
    public string ExePathHash { get; }

    /// <summary>
    /// Nome de exibição da aplicação
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Categoria de produtividade da aplicação
    /// </summary>
    public AppCategory Category { get; private set; }

    private AppIdentity() { }

    public AppIdentity(
        string exePathHash,
        string displayName,
        AppCategory? category = null)
        : base(Guid.NewGuid())
    {
        if (string.IsNullOrWhiteSpace(exePathHash))
            throw DomainException.RequiredField(nameof(exePathHash));

        ExePathHash = exePathHash;
        DisplayName = displayName ?? string.Empty;
        Category = category ?? AppCategory.Unknown;
    }

    /// <summary>
    /// Atualiza a categoria da aplicação
    /// </summary>
    public void UpdateCategory(AppCategory category)
    {
        Category = category ?? throw new ArgumentNullException(nameof(category));
    }

    public override string ToString()
        => $"{DisplayName} ({ExePathHash[..8]}...)";
}
