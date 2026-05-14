namespace TimeTrack.Backend.Application.Common.Interfaces;

public interface IPlatformApiKeyHasher
{
    /// <summary>
    /// Generates a new opaque API token (plaintext). Store only its hash.
    /// </summary>
    string GenerateToken();

    /// <summary>
    /// Hashes the plaintext token for persistence/lookup.
    /// </summary>
    string HashToken(string token);
}

