using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Infrastructure.Services;
using TimeTrack.DesktopHost.Configuration;
using TimeTrack.DesktopHost.Ipc;
using TimeTrack.DesktopHost.Notifications;
using TimeTrack.DesktopHost.Reporting;
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
    private static Mutex? _singleInstanceMutex;
    private static ActivationPipeServer? _activationServer;
    private static MainForm? _mainForm;
    private static volatile bool _pendingActivate;

    [STAThread]
    static void Main(string[] args)
    {
        var startMinimizedArg = args.Any(a => a.Equals("--start-minimized", StringComparison.OrdinalIgnoreCase));
        if (startMinimizedArg)
        {
            // Our config binding reads DesktopHost:StartMinimized from env/appsettings.
            // Task Scheduler launches the app with --start-minimized, so translate it
            // into the config key expected by DesktopHostSettings.
            Environment.SetEnvironmentVariable("DesktopHost__StartMinimized", "true");
        }

        // Enforce single-instance *before* any expensive startup work.
        if (!TryAcquireSingleInstanceMutex(out _singleInstanceMutex))
        {
            // A previous instance is already running (or still loading). Ask it to activate.
            // If this launch is from auto-start, don't steal focus.
            var message = startMinimizedArg ? "activate-no-focus" : "activate";
            ActivationPipeClient.TrySend(message);
            return;
        }

        // Start activation server early so double-clicks during startup reliably focus the first instance.
        _activationServer = new ActivationPipeServer(message =>
        {
            if (string.Equals(message, "activate", StringComparison.OrdinalIgnoreCase))
            {
                _pendingActivate = true;
                var form = _mainForm;
                if (form != null && !form.IsDisposed)
                {
                    form.BeginInvoke(new Action(form.ShowWindow));
                }
            }
        });
        _activationServer.Start();

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
            _mainForm = _host.Services.GetRequiredService<MainForm>();
            var trayIcon = _host.Services.GetRequiredService<TrayIconManager>();
            var floatingBar = _host.Services.GetRequiredService<FloatingStatusBarManager>();

            // If a second launch requested activation while we were still starting, honor it now.
            if (_pendingActivate && _mainForm != null && !_mainForm.IsDisposed)
            {
                _mainForm.BeginInvoke(new Action(_mainForm.ShowWindow));
            }

            // Run application
            Application.Run(_mainForm);
        }
        catch (Exception ex)
        {
            try
            {
                var fileSink = new ExceptionFileSink();
                var report = ExceptionReport.FromException(ex, "DesktopHost", "desktophost.startup");
                fileSink.WriteAsync(report, CancellationToken.None).GetAwaiter().GetResult();
            }
            catch
            {
                // ignore
            }

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
            _activationServer?.Dispose();
            _singleInstanceMutex?.Dispose();
        }
    }

    private static bool TryAcquireSingleInstanceMutex(out Mutex? mutex)
    {
        mutex = null;
        try
        {
            // "Local\" is per-session; that's fine because DesktopHost runs in the interactive session.
            mutex = new Mutex(initiallyOwned: true, name: @"Local\ChronosX.TimeTrack.DesktopHost", createdNew: out var createdNew);
            if (!createdNew)
            {
                mutex.Dispose();
                mutex = null;
            }
            return createdNew;
        }
        catch
        {
            // If the mutex fails for any reason, do not risk multiple instances.
            mutex?.Dispose();
            mutex = null;
            return false;
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureLogging(logging =>
            {
                // Remove EventLog provider added by default builder — requires admin and
                // throws "EventLog access is not supported on this platform" without it.
                logging.ClearProviders();
                logging.AddConsole();
                logging.AddDebug();
            })
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
                config.AddJsonFile(
                    $"appsettings.{context.HostingEnvironment.EnvironmentName}.json",
                    optional: true,
                    reloadOnChange: false);
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

                // Notification Services (Toast)
                // SRP: ToastActivationHandler apenas processa cliques
                // DIP: INotificationService permite injeção de diferentes implementações
                services.AddSingleton<ToastActivationHandler>();
                services.AddSingleton<INotificationService, WindowsToastNotificationService>();

                // Notification Event Handler - routes IPC notification events to Windows toasts
                // CX-139: Integration with Focus Mode notifications
                services.AddHostedService<NotificationEventHandler>();

                // WebView2 Bridge
                services.AddSingleton<WebViewBridge>();

                // UI Components
                services.AddSingleton<MainForm>();
                services.AddSingleton<TrayIconManager>();
                services.AddSingleton<FloatingStatusBarManager>();

                // Exception reporting (CX-250)
                services.AddSingleton<ExceptionFileSink>();
                services.AddSingleton<DesktopHostExceptionReporter>();
            });
}

internal sealed class ActivationPipeServer : IDisposable
{
    private const string PipeName = "ChronosX.TimeTrack.DesktopHost.Activation";
    private readonly Action<string> _onMessage;
    private readonly CancellationTokenSource _cts = new();
    private Task? _loopTask;

    public ActivationPipeServer(Action<string> onMessage)
    {
        _onMessage = onMessage;
    }

    public void Start()
    {
        _loopTask = Task.Run(ListenLoopAsync);
    }

    private async Task ListenLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);

                using var reader = new StreamReader(server, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 256, leaveOpen: true);
                var msg = await reader.ReadLineAsync().ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(msg))
                    _onMessage(msg.Trim());
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Best-effort: keep listening even if one connection fails.
                await Task.Delay(100, _cts.Token).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _loopTask?.Wait(500); } catch { }
        _cts.Dispose();
    }
}

internal static class ActivationPipeClient
{
    private const string PipeName = "ChronosX.TimeTrack.DesktopHost.Activation";

    public static bool TrySend(string message)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            client.Connect(timeout: 250);
            using var writer = new StreamWriter(client, Encoding.UTF8, bufferSize: 256, leaveOpen: true) { AutoFlush = true };
            writer.WriteLine(message);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
