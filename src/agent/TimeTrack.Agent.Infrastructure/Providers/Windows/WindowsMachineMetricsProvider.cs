using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;

namespace TimeTrack.Agent.Infrastructure.Providers.Windows;

/// <summary>
/// Coleta métricas de CPU, memória e disco via P/Invoke (kernel32).
/// Overhead desprezível: syscalls leves sem WMI ou PerformanceCounter.
/// </summary>
public sealed class WindowsMachineMetricsProvider : IMachineMetricsProvider
{
    private readonly ILogger<WindowsMachineMetricsProvider> _logger;

    // Previous CPU sample for delta calculation
    private long _prevIdleTime;
    private long _prevKernelTime;
    private long _prevUserTime;
    private bool _hasPreviousSample;

    public WindowsMachineMetricsProvider(ILogger<WindowsMachineMetricsProvider> logger)
    {
        _logger = logger;
    }

    public async Task<MachineMetricsReading> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var cpu = await GetCpuPercentAsync(cancellationToken);
        var (memUsed, memTotal) = GetMemoryInfo();
        var (diskUsed, diskTotal) = GetDiskInfo();

        return new MachineMetricsReading
        {
            CpuPercent = Math.Round(cpu, 1),
            MemoryUsedMb = memUsed,
            MemoryTotalMb = memTotal,
            DiskUsedGb = Math.Round(diskUsed, 2),
            DiskTotalGb = Math.Round(diskTotal, 2),
            SampledAtUtc = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Calcula CPU % usando delta de GetSystemTimes entre chamadas.
    /// Na primeira chamada, aguarda 500ms para obter dois samples.
    /// Nas chamadas subsequentes, usa o delta desde a última chamada.
    /// </summary>
    private async Task<double> GetCpuPercentAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!_hasPreviousSample)
            {
                // First sample: need two readings 500ms apart
                if (!GetSystemTimes(out var firstIdle, out var firstKernel, out var firstUser))
                    return 0;

                await Task.Delay(500, cancellationToken);

                if (!GetSystemTimes(out var secondIdle, out var secondKernel, out var secondUser))
                    return 0;

                _prevIdleTime = secondIdle;
                _prevKernelTime = secondKernel;
                _prevUserTime = secondUser;
                _hasPreviousSample = true;

                return CalculateCpuPercent(
                    firstIdle, firstKernel, firstUser,
                    secondIdle, secondKernel, secondUser);
            }

            // Subsequent calls: delta from previous sample
            if (!GetSystemTimes(out var idle, out var kernel, out var user))
                return 0;

            var cpu = CalculateCpuPercent(
                _prevIdleTime, _prevKernelTime, _prevUserTime,
                idle, kernel, user);

            _prevIdleTime = idle;
            _prevKernelTime = kernel;
            _prevUserTime = user;

            return cpu;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read CPU metrics");
            return 0;
        }
    }

    private static double CalculateCpuPercent(
        long prevIdle, long prevKernel, long prevUser,
        long currIdle, long currKernel, long currUser)
    {
        var idleDelta = currIdle - prevIdle;
        var kernelDelta = currKernel - prevKernel;
        var userDelta = currUser - prevUser;

        var totalDelta = kernelDelta + userDelta;
        if (totalDelta == 0) return 0;

        // Kernel time includes idle time, so active = total - idle
        var activePercent = (1.0 - ((double)idleDelta / totalDelta)) * 100.0;
        return Math.Clamp(activePercent, 0, 100);
    }

    private static (long usedMb, long totalMb) GetMemoryInfo()
    {
        try
        {
            var memInfo = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (!GlobalMemoryStatusEx(ref memInfo))
                return (0, 0);

            var totalMb = (long)(memInfo.ullTotalPhys / (1024 * 1024));
            var availMb = (long)(memInfo.ullAvailPhys / (1024 * 1024));
            return (totalMb - availMb, totalMb);
        }
        catch
        {
            return (0, 0);
        }
    }

    private static (double usedGb, double totalGb) GetDiskInfo()
    {
        try
        {
            double totalGb = 0;
            double usedGb = 0;

            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                    continue;

                totalGb += drive.TotalSize / (1024.0 * 1024 * 1024);
                usedGb += (drive.TotalSize - drive.AvailableFreeSpace) / (1024.0 * 1024 * 1024);
            }

            return (usedGb, totalGb);
        }
        catch
        {
            return (0, 0);
        }
    }

    #region P/Invoke

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out long lpIdleTime,
        out long lpKernelTime,
        out long lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    #endregion
}
