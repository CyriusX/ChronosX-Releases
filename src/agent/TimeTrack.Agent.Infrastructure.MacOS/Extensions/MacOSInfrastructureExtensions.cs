using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Infrastructure.MacOS.Notifications;
using TimeTrack.Agent.Infrastructure.MacOS.Providers;
using TimeTrack.Agent.Infrastructure.MacOS.Security;
using TimeTrack.Agent.Infrastructure.Services;

namespace TimeTrack.Agent.Infrastructure.MacOS.Extensions;

/// <summary>
/// Extension methods para configurar serviços de infraestrutura do macOS
/// </summary>
[SupportedOSPlatform("macos")]
public static class MacOSInfrastructureExtensions
{
    /// <summary>
    /// Adiciona providers do macOS (ActiveWindow, IdleDetector, MachineMetrics)
    /// </summary>
    public static IServiceCollection AddMacOSProviders(this IServiceCollection services)
    {
        services.AddSingleton<IFilePathExtractor, MacOSFilePathExtractor>();
        services.AddSingleton<IActiveWindowProvider, MacOSActiveWindowProvider>();
        services.AddSingleton<IIdleDetector, MacOSIdleDetector>();
        services.AddSingleton<IMachineMetricsProvider, MacOSMachineMetricsProvider>();
        services.AddSingleton<INotificationService, MacOSNotificationService>();

        services.Configure<ActiveWindowProviderOptions>(options =>
        {
            options.CacheValidityMs = 1500;
        });

        services.Configure<IdleDetectorOptions>(options =>
        {
            options.DefaultThresholdSeconds = 180;
            options.MinThresholdSeconds = 60;
            options.MaxThresholdSeconds = 600;
            options.PollingIntervalMs = 2000;
        });

        return services;
    }

    /// <summary>
    /// Adiciona serviços de sincronização com o backend para macOS
    /// </summary>
    public static IServiceCollection AddMacOSSyncServices(
        this IServiceCollection services,
        SyncSettings settings)
    {
        if (settings == null)
            throw new ArgumentNullException(nameof(settings));

        services.AddSingleton(settings);

        services.AddHttpClient("TokenStore", client =>
        {
            client.BaseAddress = new Uri(settings.BackendUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
        });

        services.AddSingleton<ITokenStore>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("TokenStore");
            var logger = sp.GetRequiredService<ILogger<KeychainTokenStore>>();
            return new KeychainTokenStore(logger, httpClient, settings);
        });

        // JwtCurrentUserContext reads the JWT from ITokenStore and extracts the
        // authenticated user's Guid — TrackingWorker gates on this. Without it,
        // the macOS DI container would fail to resolve ICurrentUserContext.
        services.AddSingleton<ICurrentUserContext, JwtCurrentUserContext>();

        services.AddHttpClient<ISyncTransport, HttpSyncTransport>(client =>
        {
            client.BaseAddress = new Uri(settings.BackendUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(settings.AuthToken))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.AuthToken);
            }
        });

        services.AddHttpClient<IDeviceActivationService, DeviceActivationService>(client =>
        {
            client.BaseAddress = new Uri(settings.BackendUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
        });

        services.AddHttpClient<IBackendReportsClient, BackendReportsClient>(client =>
        {
            client.BaseAddress = new Uri(settings.BackendUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
        });

        services.AddHttpClient<IBackendTasksClient, BackendTasksClient>(client =>
        {
            client.BaseAddress = new Uri(settings.BackendUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
        });

        services.AddHttpClient<IBackendOrgPoliciesClient, BackendOrgPoliciesClient>(client =>
        {
            client.BaseAddress = new Uri(settings.BackendUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.HttpTimeoutSeconds);
        });

        return services;
    }

}
