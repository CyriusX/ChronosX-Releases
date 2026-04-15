using System.Text;
using Microsoft.AspNetCore.DataProtection;
using TimeTrack.Backend.Application.Integrations;

namespace TimeTrack.Backend.Infrastructure.Integrations;

/// <summary>
/// Wraps ASP.NET Core's <see cref="IDataProtector"/> with a stable purpose
/// string so the token ciphertext can always be decrypted by future versions
/// of the backend. The data protection key ring is configured by ASP.NET
/// Core's standard wireup.
/// </summary>
public sealed class UserIntegrationTokenProtector : IUserIntegrationTokenProtector
{
    private const string Purpose = "TimeTrack.UserIntegration.Token.v1";

    private readonly IDataProtector _protector;

    public UserIntegrationTokenProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose);
    }

    public byte[] Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            throw new ArgumentException("Plain text token is required", nameof(plainText));
        var bytes = Encoding.UTF8.GetBytes(plainText);
        return _protector.Protect(bytes);
    }

    public string Unprotect(byte[] cipherText)
    {
        if (cipherText is null || cipherText.Length == 0)
            throw new ArgumentException("Cipher text is required", nameof(cipherText));
        var bytes = _protector.Unprotect(cipherText);
        return Encoding.UTF8.GetString(bytes);
    }
}
