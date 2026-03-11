using System;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;
using TimeTrack.Agent.Infrastructure.Extensions;
using TimeTrack.Agent.Infrastructure.Providers.Windows;

namespace TimeTrack.DesktopHost;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // Build service provider
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder.AddConsole());

        // Add Windows providers (ActiveWindow + IdleDetector)
        services.AddWindowsProviders();

        // Build provider
        var serviceProvider = services.BuildServiceProvider();

        // Create and run form with injected providers
        var activeWindowProvider = serviceProvider.GetRequiredService<IActiveWindowProvider>();
        var idleDetector = serviceProvider.GetRequiredService<IIdleDetector>();
        var logger = serviceProvider.GetRequiredService<ILogger<MainForm>>();

        Application.Run(new MainForm(activeWindowProvider, idleDetector, logger));
    }
}
