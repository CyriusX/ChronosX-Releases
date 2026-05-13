using System.Text.Json.Nodes;

namespace TimeTrack.Api.OpsMcp;

public static class OpsMcpMetadataRedactor
{
    private static readonly string[] SensitiveKeyFragments =
    [
        "token",
        "secret",
        "authorization",
        "password",
        "jwt",
        "refresh",
        "access",
        "apikey",
        "api_key"
    ];

    public static JsonNode? Redact(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson)) return null;

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(metadataJson);
        }
        catch
        {
            // If it's not valid JSON, don't return raw text (could contain secrets).
            return null;
        }

        RedactNode(node);
        return node;
    }

    private static void RedactNode(JsonNode? node)
    {
        if (node is null) return;

        if (node is JsonObject obj)
        {
            var keys = obj.Select(kvp => kvp.Key).ToList();
            foreach (var key in keys)
            {
                if (IsSensitiveKey(key))
                {
                    obj[key] = "***redacted***";
                    continue;
                }

                var child = obj[key];
                if (child is JsonValue value
                    && value.TryGetValue<string>(out var s)
                    && LooksLikeJwt(s))
                {
                    obj[key] = "***redacted***";
                    continue;
                }

                RedactNode(child);
            }
            return;
        }

        if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                var child = arr[i];
                if (child is JsonValue value
                    && value.TryGetValue<string>(out var s)
                    && LooksLikeJwt(s))
                {
                    arr[i] = "***redacted***";
                    continue;
                }

                RedactNode(child);
            }
            return;
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;

        var lower = key.Trim().ToLowerInvariant();
        return SensitiveKeyFragments.Any(f => lower.Contains(f, StringComparison.Ordinal));
    }

    private static bool LooksLikeJwt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        // Very rough heuristic: 3 base64url-ish segments separated by dots.
        var parts = value.Split('.');
        if (parts.Length != 3) return false;
        return parts.All(p => p.Length >= 10 && p.All(ch =>
            char.IsLetterOrDigit(ch) || ch is '-' or '_' ));
    }
}
