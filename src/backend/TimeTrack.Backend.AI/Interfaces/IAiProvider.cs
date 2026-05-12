namespace TimeTrack.Backend.AI.Interfaces;

public interface IAiProvider
{
    string ProviderName { get; }

    Task<AiProviderResponse> SendChatAsync(
        string systemPrompt,
        string userPrompt,
        string model,
        CancellationToken ct);
}

public sealed class AiProviderResponse
{
    public string Content { get; init; } = string.Empty;
    public int? TokensUsed { get; init; }
    public int LatencyMs { get; init; }
    public string ModelUsed { get; init; } = string.Empty;
}
