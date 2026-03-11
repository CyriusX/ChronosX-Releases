namespace TimeTrack.Backend.Application.Common.Exceptions;

public class NotFoundException : Exception
{
    public string EntityType { get; }
    public object Key { get; }

    public NotFoundException(string entityType, object key)
        : base($"{entityType} with key '{key}' was not found.")
    {
        EntityType = entityType;
        Key = key;
    }

    public NotFoundException(string entityType, object key, Exception innerException)
        : base($"{entityType} with key '{key}' was not found.", innerException)
    {
        EntityType = entityType;
        Key = key;
    }
}

public class ForbiddenException : Exception
{
    public string Reason { get; }

    public ForbiddenException(string reason)
        : base($"Access forbidden: {reason}")
    {
        Reason = reason;
    }
}

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation failures have occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string property, string error)
        : this(new Dictionary<string, string[]> { [property] = new[] { error } })
    {
    }
}

public class ConflictException : Exception
{
    public string Code { get; }

    public ConflictException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}
