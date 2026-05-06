namespace TimeTrack.Backend.Application.Common.Exceptions;

/// <summary>
/// Exception thrown when a resource is not found
/// </summary>
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

/// <summary>
/// Exception thrown when access is forbidden
/// </summary>
public class ForbiddenException : Exception
{
    public string Reason { get; }

    public ForbiddenException(string reason)
        : base($"Access forbidden: {reason}")
    {
        Reason = reason;
    }
}

/// <summary>
/// Exception thrown when validation fails
/// </summary>
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

/// <summary>
/// Exception thrown when there's a conflict (e.g., duplicate resource)
/// </summary>
public class ConflictException : Exception
{
    public string Code { get; }

    public ConflictException(string code, string message)
        : base(message)
    {
        Code = code;
    }
}

/// <summary>
/// Exception thrown when a user account is deactivated.
/// Returns 401 Unauthorized with error code "user_deactivated".
/// </summary>
public class UserDeactivatedException : Exception
{
    public string Code => "user_deactivated";

    public UserDeactivatedException(string message = "User account is deactivated")
        : base(message)
    {
    }
}

/// <summary>
/// Exception thrown when an active subscription is required.
/// Returns 402 Payment Required.
/// </summary>
public class SubscriptionRequiredException : Exception
{
    public string Code => "subscription_required";

    public SubscriptionRequiredException(string message = "Active subscription required")
        : base(message)
    {
    }
}

/// <summary>
/// Exception thrown when a subscription limit is exceeded.
/// Returns 402 Payment Required.
/// </summary>
public class SubscriptionLimitExceededException : Exception
{
    public string Code => "subscription_limit_exceeded";
    public string LimitType { get; }

    public SubscriptionLimitExceededException(string limitType, string message)
        : base(message)
    {
        LimitType = limitType;
    }
}
