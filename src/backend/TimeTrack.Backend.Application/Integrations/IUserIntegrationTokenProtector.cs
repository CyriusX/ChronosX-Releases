namespace TimeTrack.Backend.Application.Integrations;

/// <summary>
/// Symmetric encryption for per-user third-party API tokens. Application
/// layer consumers never see raw <c>IDataProtector</c> — they go through
/// this interface so the use cases remain infrastructure-agnostic.
/// </summary>
public interface IUserIntegrationTokenProtector
{
    /// <summary>Encrypt a plain text API key for at-rest storage.</summary>
    byte[] Protect(string plainText);

    /// <summary>Decrypt an encrypted token back to its plain text form.</summary>
    string Unprotect(byte[] cipherText);
}
