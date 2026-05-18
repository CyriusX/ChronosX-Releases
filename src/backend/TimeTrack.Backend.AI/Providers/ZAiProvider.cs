using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.AI.Interfaces;

namespace TimeTrack.Backend.AI.Providers;

public sealed class ZAiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly ZAiOptions _options;
    private readonly ILogger<ZAiProvider> _logger;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public string ProviderName => "z.ai";

    public ZAiProvider(
        HttpClient httpClient,
        IOptions<ZAiOptions> options,
        ILogger<ZAiProvider> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = _options.MaxRetries,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => !r.IsSuccessStatusCode && ((int)r.StatusCode == 429 || (int)r.StatusCode >= 500)),
                OnRetry = args =>
                {
                    _logger.LogWarning("z.ai retry attempt {Attempt} after {Delay}ms",
                        args.AttemptNumber, args.RetryDelay.TotalMilliseconds);
                    return default;
                }
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 1.0,
                SamplingDuration = TimeSpan.FromSeconds(60),
                MinimumThroughput = _options.CircuitBreakerFailures,
                BreakDuration = TimeSpan.FromSeconds(_options.CircuitBreakerDurationSeconds),
                OnOpened = args =>
                {
                    _logger.LogWarning("z.ai circuit breaker OPENED for {Duration}s",
                        _options.CircuitBreakerDurationSeconds);
                    return default;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation("z.ai circuit breaker CLOSED - service recovered");
                    return default;
                }
            })
            .Build();
    }

    public async Task<AiProviderResponse> SendChatAsync(
        string systemPrompt,
        string userPrompt,
        string model,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        var messages = new object[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt }
        };

        var requestBody = new { model, messages };
        var json = JsonSerializer.Serialize(requestBody, JsonOptions);

        var response = await _pipeline.ExecuteAsync(async token =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");

            return await _httpClient.SendAsync(request, token);
        }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("z.ai returned {StatusCode}: {Body}",
                (int)response.StatusCode, errorBody);
        }

        response.EnsureSuccessStatusCode();

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        sw.Stop();

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        var assistantContent = root
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;

        int? tokensUsed = null;
        if (root.TryGetProperty("usage", out var usage) &&
            usage.TryGetProperty("total_tokens", out var tt))
        {
            tokensUsed = tt.GetInt32();
        }

        return new AiProviderResponse
        {
            Content = assistantContent,
            TokensUsed = tokensUsed,
            LatencyMs = (int)sw.ElapsedMilliseconds,
            ModelUsed = model
        };
    }
}
