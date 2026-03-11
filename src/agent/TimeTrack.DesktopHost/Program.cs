using System;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.DesktopHost.Configuration;
using TimeTrack.DesktopHost.Ipc;
using TimeTrack.DesktopHost.UI;

namespace TimeTrack.DesktopHost;

/// <summary>
/// Entry point for TimeTrack Desktop Host
/// Hosts WebView2 UI and bridges IPC between React UI and AgentService
/// </summary>
static class Program
{
    private static IHost? _host;
    private static ILogger? _logger;

    [STAThread]
    static void Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            // Build DI container
            _host = CreateHostBuilder(args).Build();
            _logger = _host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("DesktopHost");

            _logger.LogInformation("TimeTrack DesktopHost starting...");

            // Start hosted services (IPC client, etc.)
            _host.Start();

            // Get main form from DI
            var mainForm = _host.Services.GetRequiredService<MainForm>();
            var trayIcon = _host.Services.GetRequiredService<TrayIconManager>();

            // Run application
            Application.Run(mainForm);
        }
        catch (Exception ex)
        {
            _logger?.LogCritical(ex, "Fatal error starting DesktopHost");
            MessageBox.Show(
                $"Fatal error: {ex.Message}",
                "TimeTrack Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            // Stop hosted services gracefully
            _host?.StopAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            _host?.Dispose();
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile("appsettings.json", optional: true);
                config.AddEnvironmentVariables();
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Bind configuration to settings
                var settings = new DesktopHostSettings();
                context.Configuration.GetSection("DesktopHost").Bind(settings);
                services.AddSingleton(settings);

                // IPC Client (Named Pipe to AgentService)
                services.AddSingleton<IIpcClient, NamedPipeIpcClient>();
                services.AddHostedService<IpcClientHostedService>(sp =>
                    (IpcClientHostedService)sp.GetRequiredService<IIpcClient>());

                // WebView2 Bridge
                services.AddSingleton<WebViewBridge>();

                // UI Components
                services.AddSingleton<MainForm>();
                services.AddSingleton<TrayIconManager>();
            });
}
