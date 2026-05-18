using System.Security.Cryptography;
using System.Text;
using TimeTrack.Backend.Application.Common.Interfaces;

namespace TimeTrack.Backend.Infrastructure.Services;

public sealed class PlatformApiKeyHasher : IPlatformApiKeyHasher
{
    private const string TokenPrefix = "tt_ops_";

    public string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);

        // URL-safe base64 (no padding)
        var token = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return TokenPrefix + token;
    }

    public string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token is required", nameof(token));

        // SHA-256 hex lowercase
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(token.Trim()));
        var sb = new StringBuilder(hashBytes.Length * 2);
        foreach (var b in hashBytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}

