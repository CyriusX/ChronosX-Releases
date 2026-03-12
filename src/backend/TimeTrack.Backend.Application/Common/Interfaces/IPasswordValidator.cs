namespace TimeTrack.Backend.Application.Common.Interfaces;

/// <summary>
/// Interface para validação de complexidade de senhas
/// </summary>
public interface IPasswordValidator
{
    /// <summary>
    /// Valida a complexidade da senha
    /// </summary>
    /// <param name="password">Senha a validar</param>
    /// <returns>Resultado da validação com erros se houver</returns>
    PasswordValidationResult Validate(string password);
}

/// <summary>
/// Resultado da validação de senha
/// </summary>
public sealed class PasswordValidationResult
{
    public bool IsValid { get; init; }
    public string[] Errors { get; init; } = Array.Empty<string>();

    public static PasswordValidationResult Success() => new() { IsValid = true };

    public static PasswordValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors
    };
}
