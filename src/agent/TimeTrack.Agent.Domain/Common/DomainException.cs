namespace TimeTrack.Agent.Domain.Common;

/// <summary>
/// Exceção lançada quando uma invariante de domínio é violada
/// </summary>
public class DomainException : Exception
{
    public string Code { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public DomainException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public static DomainException RequiredField(string fieldName)
        => new("REQUIRED_FIELD", $"O campo '{fieldName}' é obrigatório.");

    public static DomainException InvalidTimeRange()
        => new("INVALID_TIME_RANGE", "O tempo de término deve ser maior que o tempo de início.");
}
