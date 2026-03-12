using TimeTrack.Backend.Application.Common.Interfaces;

namespace TimeTrack.Backend.Infrastructure.Services;

/// <summary>
/// Validador de complexidade de senhas
/// Requisitos: 8+ chars, 1 maiúscula, 1 número
/// </summary>
public sealed class PasswordValidator : IPasswordValidator
{
    private const int MinimumLength = 8;

    public PasswordValidationResult Validate(string password)
    {
        var errors = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            return PasswordValidationResult.Failure("Password is required");
        }

        if (password.Length < MinimumLength)
        {
            errors.Add($"Password must be at least {MinimumLength} characters long");
        }

        if (!password.Any(char.IsUpper))
        {
            errors.Add("Password must contain at least one uppercase letter");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add("Password must contain at least one number");
        }

        return errors.Count == 0
            ? PasswordValidationResult.Success()
            : PasswordValidationResult.Failure(errors.ToArray());
    }
}
