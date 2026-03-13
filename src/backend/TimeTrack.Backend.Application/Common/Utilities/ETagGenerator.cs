using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TimeTrack.Backend.Application.Common.Utilities;

/// <summary>
/// Utility class for generating ETags for HTTP caching
/// </summary>
public static class ETagGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Generates an ETag based on the hash of the serialized object
    /// </summary>
    /// <typeparam name="T">Type of the object to hash</typeparam>
    /// <param name="data">The object to generate ETag for</param>
    /// <returns>MD5 hash as lowercase hex string</returns>
    public static string Generate<T>(T data)
    {
        var json = JsonSerializer.Serialize(data, JsonOptions);
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Checks if the client's ETag matches the generated ETag
    /// </summary>
    /// <param name="clientETag">The ETag sent by the client (If-None-Match header)</param>
    /// <param name="currentETag">The current ETag generated from data</param>
    /// <returns>True if ETags match (content not modified)</returns>
    public static bool Matches(string? clientETag, string currentETag)
    {
        if (string.IsNullOrEmpty(clientETag))
            return false;

        return clientETag.Trim('"') == currentETag;
    }
}
