using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Infrastructure.Services;
using TimeTrack.DesktopHost.Ipc;

namespace TimeTrack.DesktopHost.Reporting;

/// <summary>
/// DesktopHost-side reporter:
/// - Always writes to the local daily JSONL exception log (ProgramData).
/// - Best-effort forwards to AgentService via IPC so it can emit a maintenance event.
/// Must never throw to callers.
/// </summary>
public sealed class DesktopHostExceptionReporter
{
    private readonly ExceptionFileSink _fileSink;
    private readonly IIpcClient _ipcClient;

    public DesktopHostExceptionReporter(ExceptionFileSink fileSink, IIpcClient ipcClient)
    {
        _fileSink = fileSink;
        _ipcClient = ipcClient;
    }

    public async Task ReportAsync(Exception ex, string operation, Dictionary<string, string?>? context = null)
    {
        try
        {
            if (ExceptionReport.IsCancellation(ex)) return;

            var report = ExceptionReport.FromException(
                ex,
                component: "DesktopHost",
                operation: operation,
                context: context);

            await _fileSink.WriteAsync(report, CancellationToken.None).ConfigureAwait(false);

            if (_ipcClient.IsConnected)
            {
                _ = await _ipcClient.SendCommandAsync("ReportException", report, CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }
        catch
        {
            // Best-effort: never throw
        }
    }
}

