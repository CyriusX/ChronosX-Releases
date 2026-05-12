using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.AI.Interfaces;
using TimeTrack.Backend.AI.Providers;
using TimeTrack.Backend.AI.Services;

namespace TimeTrack.Backend.AI.Extensions;

public static class AiServiceExtensions
{
    public static IServiceCollection AddAiModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Phase 1 options
        services.Configure<ZAiOptions>(configuration.GetSection(ZAiOptions.SectionName));
        services.Configure<LiveInsightOptions>(configuration.GetSection(LiveInsightOptions.SectionName));

        // Phase 3 options
        services.Configure<AnomalyDetectionOptions>(configuration.GetSection(AnomalyDetectionOptions.SectionName));
        services.Configure<PatternDetectionOptions>(configuration.GetSection(PatternDetectionOptions.SectionName));
        services.Configure<ClassificationJobOptions>(configuration.GetSection(ClassificationJobOptions.SectionName));
        services.Configure<NarrativeJobOptions>(configuration.GetSection(NarrativeJobOptions.SectionName));
        services.Configure<AlertGenerationOptions>(configuration.GetSection(AlertGenerationOptions.SectionName));
        services.Configure<InsightThresholdsOptions>(configuration.GetSection(InsightThresholdsOptions.SectionName));

        services.AddMemoryCache();

        // Provider (HTTP + Polly resilience)
        services.AddHttpClient<IAiProvider, ZAiProvider>(client =>
        {
            var baseUrl = configuration[$"{ZAiOptions.SectionName}:BaseUrl"]
                ?? configuration["ZAI_BASE_URL"]
                ?? throw new InvalidOperationException("z.ai BaseUrl not configured. Set ZAi:BaseUrl or ZAI_BASE_URL");

            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(
                int.TryParse(configuration[$"{ZAiOptions.SectionName}:TimeoutSeconds"], out var timeout)
                    ? timeout : 10);
        });

        // Internal services
        services.AddScoped<AiDecisionLogger>();
        services.AddSingleton<ClassificationCache>();

        // Facade
        services.AddScoped<IAIService, ZAiService>();

        return services;
    }
}
