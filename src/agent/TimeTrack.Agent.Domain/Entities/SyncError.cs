namespace TimeTrack.Agent.Domain.Entities;

/// <summary>
/// Representa um erro de sincronização registrado localmente
/// </summary>
public sealed class SyncError
{
    public Guid Id { get; private set; }
    public DateTime TimestampUtc { get; private set; }
    public string Endpoint { get; private set; } = string.Empty;
    public int StatusCode { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;
    public int AttemptCount { get; private set; }

    private SyncError() { }

    public static SyncError Create(
        string endpoint,
        int statusCode,
        string errorMessage,
        int attemptCount)
    {
        return new SyncError
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTime.UtcNow,
            Endpoint = endpoint,
            StatusCode = statusCode,
            ErrorMessage = errorMessage,
            AttemptCount = attemptCount
        };
    }
}
