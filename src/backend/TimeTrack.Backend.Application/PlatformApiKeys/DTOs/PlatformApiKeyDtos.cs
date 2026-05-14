namespace TimeTrack.Backend.Application.PlatformApiKeys.DTOs;

public class PlatformApiKeyDto
{
    public Guid Id { get; init; }
    public string Label { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? RevokedAtUtc { get; init; }
    public DateTime? LastUsedAtUtc { get; init; }
}

public sealed class CreatePlatformApiKeyResponse : PlatformApiKeyDto
{
    /// <summary>Plaintext token; only returned once at creation time.</summary>
    public string Token { get; init; } = string.Empty;
}
