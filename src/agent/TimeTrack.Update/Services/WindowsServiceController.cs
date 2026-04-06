using System.ServiceProcess;
using Microsoft.Extensions.Logging;

namespace TimeTrack.Update.Services;

/// <summary>
/// Controls Windows services using System.ServiceProcess
/// </summary>
public sealed class WindowsServiceController : IServiceController
{
    private readonly ILogger<WindowsServiceController> _logger;

    public WindowsServiceController(ILogger<WindowsServiceController> logger)
    {
        _logger = logger;
    }

    public async Task<bool> StopServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var controller = new ServiceController(serviceName);

            if (controller.Status != ServiceControllerStatus.Running &&
                controller.Status != ServiceControllerStatus.StartPending)
            {
                _logger.LogInformation("Service {Service} is not running (status: {Status})", serviceName, controller.Status);
                return true;
            }

            _logger.LogInformation("Stopping service: {Service}", serviceName);
            controller.Stop();

            await WaitForStatusAsync(serviceName, ServiceStatus.Stopped, timeout, cancellationToken);

            _logger.LogInformation("Service {Service} stopped successfully", serviceName);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Service {Service} not found or cannot be controlled", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop service {Service}", serviceName);
            return false;
        }
    }

    public async Task<bool> StartServiceAsync(string serviceName, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var controller = new ServiceController(serviceName);

            if (controller.Status == ServiceControllerStatus.Running)
            {
                _logger.LogInformation("Service {Service} is already running", serviceName);
                return true;
            }

            _logger.LogInformation("Starting service: {Service}", serviceName);
            controller.Start();

            await WaitForStatusAsync(serviceName, ServiceStatus.Running, timeout, cancellationToken);

            _logger.LogInformation("Service {Service} started successfully", serviceName);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Service {Service} not found or cannot be controlled", serviceName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start service {Service}", serviceName);
            return false;
        }
    }

    public ServiceStatus GetServiceStatus(string serviceName)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            return ConvertStatus(controller.Status);
        }
        catch
        {
            return ServiceStatus.Stopped;
        }
    }

    public async Task<bool> WaitForStatusAsync(string serviceName, ServiceStatus desiredStatus, TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            using var controller = new ServiceController(serviceName);
            var targetStatus = ConvertToServiceControllerStatus(desiredStatus);
            var deadline = DateTime.UtcNow + timeout;

            while (controller.Status != targetStatus)
            {
                if (DateTime.UtcNow > deadline)
                {
                    _logger.LogWarning("Timeout waiting for service {Service} to reach status {Status}", serviceName, desiredStatus);
                    return false;
                }

                cancellationToken.ThrowIfCancellationRequested();

                await Task.Delay(500, cancellationToken);
                controller.Refresh();
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiting for service {Service} status", serviceName);
            return false;
        }
    }

    private static ServiceStatus ConvertStatus(ServiceControllerStatus status)
    {
        return status switch
        {
            ServiceControllerStatus.Stopped => ServiceStatus.Stopped,
            ServiceControllerStatus.StartPending => ServiceStatus.StartPending,
            ServiceControllerStatus.StopPending => ServiceStatus.StopPending,
            ServiceControllerStatus.Running => ServiceStatus.Running,
            ServiceControllerStatus.ContinuePending => ServiceStatus.ContinuePending,
            ServiceControllerStatus.PausePending => ServiceStatus.PausePending,
            ServiceControllerStatus.Paused => ServiceStatus.Paused,
            _ => ServiceStatus.Stopped
        };
    }

    private static ServiceControllerStatus ConvertToServiceControllerStatus(ServiceStatus status)
    {
        return status switch
        {
            ServiceStatus.Stopped => ServiceControllerStatus.Stopped,
            ServiceStatus.StartPending => ServiceControllerStatus.StartPending,
            ServiceStatus.StopPending => ServiceControllerStatus.StopPending,
            ServiceStatus.Running => ServiceControllerStatus.Running,
            ServiceStatus.ContinuePending => ServiceControllerStatus.ContinuePending,
            ServiceStatus.PausePending => ServiceControllerStatus.PausePending,
            ServiceStatus.Paused => ServiceControllerStatus.Paused,
            _ => ServiceControllerStatus.Stopped
        };
    }
}
