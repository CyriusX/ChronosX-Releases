using System.Text.Json;
using TimeTrack.Backend.AI.Interfaces;

namespace TimeTrack.Backend.AI.Services;

public static class ResponseParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static ClassificationResult ParseClassification(string rawContent, AppClassificationRequest request)
    {
        try
        {
            var cleaned = ExtractJson(rawContent);
            return JsonSerializer.Deserialize<ClassificationResult>(cleaned, JsonOptions)
                ?? FallbackClassifyApp();
        }
        catch (JsonException)
        {
            return FallbackClassifyApp();
        }
    }

    public static string ExtractJson(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0)
                trimmed = trimmed[(firstNewline + 1)..];
            if (trimmed.EndsWith("```"))
                trimmed = trimmed[..^3];
            trimmed = trimmed.Trim();
        }
        return trimmed;
    }

    public static string GetFallback(string decisionType)
    {
        return decisionType switch
        {
            "weekly_narrative" => "Resumo semanal indisponivel no momento.",
            "pattern_detected" => "Padrao detectado. Descricao detalhada indisponivel.",
            "alert_generated" => "Alerta gerado. Detalhes indisponiveis.",
            "reports_suggestion" => "Sugestao indisponivel no momento.",
            "weekly_email_report" => "Seu relatorio semanal esta indisponivel no momento. Acesse o dashboard para ver seus dados.",
            _ => "Resposta nao disponivel."
        };
    }

    public static string EscapeForJson(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static ClassificationResult FallbackClassifyApp()
    {
        return new ClassificationResult
        {
            Category = "neutral",
            Subcategory = "unclassified",
            Confidence = 0.3,
            Reasoning = "Fallback classification - AI provider unavailable"
        };
    }
}
