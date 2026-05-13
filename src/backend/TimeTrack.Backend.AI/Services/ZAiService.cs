using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.AI.Providers;

namespace TimeTrack.Backend.AI.Services;

public sealed class ZAiService : IAIService
{
    private readonly IAiProvider _provider;
    private readonly AiDecisionLogger _decisionLogger;
    private readonly ClassificationCache _cache;
    private readonly ZAiOptions _options;
    private readonly ILogger<ZAiService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ZAiService(
        IAiProvider provider,
        AiDecisionLogger decisionLogger,
        ClassificationCache cache,
        IOptions<ZAiOptions> options,
        ILogger<ZAiService> logger)
    {
        _provider = provider;
        _decisionLogger = decisionLogger;
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ClassificationResult> ClassifyAppAsync(
        AppClassificationRequest request,
        Guid orgId,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        var cached = _cache.Get(request.ExeName);
        if (cached != null) return cached;

        var (systemPrompt, userPrompt) = PromptBuilder.BuildClassificationPrompt(request);
        var model = _options.ResolveModel("app_classification");

        try
        {
            var response = await CallAndLogAsync(
                systemPrompt, userPrompt, model, orgId, userId, "app_classification", ct);

            var classification = ResponseParser.ParseClassification(response.Content, request);
            _cache.Set(request.ExeName, classification);
            return classification;
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "AI classification failed for {ExeName}, using fallback", request.ExeName);
            return ResponseParser.ParseClassification(string.Empty, request);
        }
    }

    public void InvalidateClassificationCache(string exeName) => _cache.Invalidate(exeName);

    public async Task<string> GenerateWeeklyNarrativeAsync(
        WeeklyFeatureContext context,
        Guid orgId,
        Guid userId,
        CancellationToken ct = default)
    {
        var (systemPrompt, userPrompt) = PromptBuilder.BuildWeeklyNarrativePrompt(context);
        var model = _options.ResolveModel("weekly_narrative");

        try
        {
            var response = await CallAndLogAsync(
                systemPrompt, userPrompt, model, orgId, userId, "weekly_narrative", ct);
            return response.Content;
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Weekly narrative generation failed, using fallback");
            var fallback = ResponseParser.GetFallback("weekly_narrative");
            await LogFallbackAsync(orgId, userId, "weekly_narrative", fallback, ct);
            return fallback;
        }
    }

    public async Task<string> DescribePatternAsync(
        UserPatternContext pattern,
        Guid orgId,
        Guid userId,
        CancellationToken ct = default)
    {
        var (systemPrompt, userPrompt) = PromptBuilder.BuildPatternDescriptionPrompt(pattern);
        var model = _options.ResolveModel("pattern_detected");

        try
        {
            var response = await CallAndLogAsync(
                systemPrompt, userPrompt, model, orgId, userId, "pattern_detected", ct);
            return response.Content;
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Pattern description failed, using fallback");
            var fallback = ResponseParser.GetFallback("pattern_detected");
            await LogFallbackAsync(orgId, userId, "pattern_detected", fallback, ct);
            return fallback;
        }
    }

    public async Task<string> GenerateAlertMessageAsync(
        AlertContext context,
        Guid orgId,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        var (systemPrompt, userPrompt) = PromptBuilder.BuildAlertMessagePrompt(context);
        var model = _options.ResolveModel("alert_generated");

        try
        {
            var response = await CallAndLogAsync(
                systemPrompt, userPrompt, model, orgId, userId, "alert_generated", ct);
            return response.Content;
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Alert generation failed, using fallback");
            var fallback = ResponseParser.GetFallback("alert_generated");
            await LogFallbackAsync(orgId, userId, "alert_generated", fallback, ct);
            return fallback;
        }
    }

    public async Task<string> GenerateReportsSuggestionAsync(
        ReportsSuggestionContext context,
        Guid orgId,
        Guid? userId = null,
        CancellationToken ct = default)
    {
        var (systemPrompt, userPrompt) = PromptBuilder.BuildReportsSuggestionPrompt(context);
        var model = _options.ResolveModel("reports_suggestion");

        try
        {
            var response = await CallAndLogAsync(
                systemPrompt, userPrompt, model, orgId, userId, "reports_suggestion", ct);
            return response.Content;
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Reports suggestion failed, using fallback");
            var fallback = ResponseParser.GetFallback("reports_suggestion");
            await LogFallbackAsync(orgId, userId, "reports_suggestion", fallback, ct);
            return fallback;
        }
    }

    public async Task<string> GenerateWeeklyEmailReportAsync(
        WeeklyEmailReportContext context,
        Guid orgId,
        Guid userId,
        CancellationToken ct = default)
    {
        var (systemPrompt, userPrompt) = PromptBuilder.BuildWeeklyEmailReportPrompt(context);
        var model = _options.ResolveModel("weekly_email_report");

        try
        {
            var response = await CallAndLogAsync(
                systemPrompt, userPrompt, model, orgId, userId, "weekly_email_report", ct);
            return response.Content;
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Weekly email report generation failed, using fallback");
            var fallback = ResponseParser.GetFallback("weekly_email_report");
            await LogFallbackAsync(orgId, userId, "weekly_email_report", fallback, ct);
            return fallback;
        }
    }

    private async Task<AiProviderResponse> CallAndLogAsync(
        string systemPrompt, string userPrompt, string model,
        Guid orgId, Guid? userId, string decisionType,
        CancellationToken ct)
    {
        var response = await _provider.SendChatAsync(systemPrompt, userPrompt, model, ct);

        var inputJson = JsonDocument.Parse(
            JsonSerializer.Serialize(new { systemPrompt, userPrompt }, JsonOptions)).RootElement;
        var outputJson = JsonDocument.Parse(
            $"\"{ResponseParser.EscapeForJson(response.Content)}\"").RootElement;

        await _decisionLogger.LogAsync(
            orgId, userId, decisionType, inputJson, outputJson,
            response.ModelUsed, null, response.TokensUsed, response.LatencyMs, ct);

        return response;
    }

    private async Task LogFallbackAsync(
        Guid orgId, Guid? userId, string decisionType,
        string fallbackContent, CancellationToken ct)
    {
        try
        {
            var outputJson = JsonDocument.Parse(
                $"\"{ResponseParser.EscapeForJson(fallbackContent)}\"").RootElement;

            await _decisionLogger.LogAsync(
                orgId, userId, decisionType, default, outputJson,
                "rules-v1", null, null, 0, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log fallback for {DecisionType}", decisionType);
        }
    }
}
