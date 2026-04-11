using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Providers;

namespace TimeTrack.Agent.Infrastructure.MacOS.Providers;

/// <summary>
/// Implementation of IMachineMetricsProvider for macOS using sysctl and NSProcessInfo
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class MacOSMachineMetricsProvider : IMachineMetricsProvider
{
    private readonly ILogger<MacOSMachineMetricsProvider> _logger;

    private long _prevCpuTotal;
    private long _prevCpuUser;
    private long _prevCpuSystem;
    private long _prevCpuIdle;
    private bool _hasPreviousSample;

    public MacOSMachineMetricsProvider(ILogger<MacOSMachineMetricsProvider> logger)
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

    private async Task<double> GetCpuPercentAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!_hasPreviousSample)
            {
                if (!GetCpuTimes(out var firstTotal, out var firstUser, out var firstSystem, out var firstIdle))
                    return 0;

                await Task.Delay(500, cancellationToken);

                if (!GetCpuTimes(out var secondTotal, out var secondUser, out var secondSystem, out var secondIdle))
                    return 0;

                _prevCpuTotal = secondTotal;
                _prevCpuUser = secondUser;
                _prevCpuSystem = secondSystem;
                _prevCpuIdle = secondIdle;
                _hasPreviousSample = true;

                return CalculateCpuPercent(
                    firstTotal, firstUser, firstSystem, firstIdle,
                    secondTotal, secondUser, secondSystem, secondIdle);
            }

            if (!GetCpuTimes(out var total, out var user, out var system, out var idle))
                return 0;

            var cpu = CalculateCpuPercent(
                _prevCpuTotal, _prevCpuUser, _prevCpuSystem, _prevCpuIdle,
                total, user, system, idle);

            _prevCpuTotal = total;
            _prevCpuUser = user;
            _prevCpuSystem = system;
            _prevCpuIdle = idle;

            return cpu;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read CPU metrics");
            return 0;
        }
    }

    private static double CalculateCpuPercent(
        long prevTotal, long prevUser, long prevSystem, long prevIdle,
        long currTotal, long currUser, long currSystem, long currIdle)
    {
        var totalDelta = currTotal - prevTotal;
        if (totalDelta == 0)
            return 0;

        var idleDelta = currIdle - prevIdle;
        var activePercent = (1.0 - ((double)idleDelta / totalDelta)) * 100.0;
        return Math.Clamp(activePercent, 0, 100);
    }

    private (long usedMb, long totalMb) GetMemoryInfo()
    {
        try
        {
            var totalBytes = GetSysctl64("hw.memsize");
            var pageSize = GetSysctl64("vm.pagesize");
            var freePages = GetSysctl64("vm.page_free_count");
            var inactivePages = GetSysctl64("vm.page_inactive_count");
            var activePages = GetSysctl64("vm.page_active_count");
            var wiredPages = GetSysctl64("vm.page_wire_count");

            if (totalBytes == 0 || pageSize == 0)
                return (0, 0);

            var usedPages = activePages + wiredPages;
            var usedBytes = usedPages * pageSize;
            var totalMb = (long)(totalBytes / (1024 * 1024));
            var usedMb = (long)(usedBytes / (1024 * 1024));

            return (usedMb, totalMb);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read memory metrics");
            return (0, 0);
        }
    }

    private (double usedGb, double totalGb) GetDiskInfo()
    {
        try
        {
            double totalGb = 0;
            double usedGb = 0;

            var volumes = Directory.GetDirectories("/Volumes");
            var rootFs = GetFileSystemInfo("/");
            if (rootFs != null)
            {
                totalGb += rootFs.TotalSize / (1024.0 * 1024 * 1024);
                usedGb += (rootFs.TotalSize - rootFs.AvailableFreeSpace) / (1024.0 * 1024 * 1024);
            }

            foreach (var volume in volumes)
            {
                try
                {
                    var fs = GetFileSystemInfo(volume);
                    if (fs != null)
                    {
                        totalGb += fs.TotalSize / (1024.0 * 1024 * 1024);
                        usedGb += (fs.TotalSize - fs.AvailableFreeSpace) / (1024.0 * 1024 * 1024);
                    }
                }
                catch
                {
                    continue;
                }
            }

            return (usedGb, totalGb);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read disk metrics");
            return (0, 0);
        }
    }

    private static DriveInfo? GetFileSystemInfo(string path)
    {
        try
        {
            return new DriveInfo(path);
        }
        catch
        {
            return null;
        }
    }

    private bool GetCpuTimes(out long total, out long user, out long system, out long idle)
    {
        total = 0;
        user = 0;
        system = 0;
        idle = 0;

        try
        {
            var cpuTimes = GetCpuTimesInternal();
            if (cpuTimes == null || cpuTimes.Length == 0)
                return false;

            user = cpuTimes.Sum(ct => ct.User);
            system = cpuTimes.Sum(ct => ct.System);
            idle = cpuTimes.Sum(ct => ct.Idle);
            total = user + system + idle;

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to read CPU times");
            return false;
        }
    }

    private CpuTime[]? GetCpuTimesInternal()
    {
        try
        {
            var mib = new[] { CTL_HW, HW_CPUSTATS };
            var size = Marshal.SizeOf<CpuStats>();
            var buffer = Marshal.AllocHGlobal(size);

            try
            {
                if (sysctl(mib, 2, buffer, ref size, IntPtr.Zero, 0) != 0)
                    return null;

                var stats = Marshal.PtrToStructure<CpuStats>(buffer);

                return new[]
                {
                    new CpuTime
                    {
                        User = stats.cpu_time[0].cp_user,
                        System = stats.cpu_time[0].cp_sys,
                        Idle = stats.cpu_time[0].cp_idle
                    }
                };
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return null;
        }
    }

    private long GetSysctl64(string name)
    {
        try
        {
            var buffer = Marshal.AllocHGlobal(8);

            try
            {
                var size = 8;
                if (sysctlbyname(name, buffer, ref size, IntPtr.Zero, 0) != 0)
                    return 0;

                return Marshal.ReadInt64(buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch
        {
            return 0;
        }
    }

    private record CpuTime
    {
        public long User { get; init; }
        public long System { get; init; }
        public long Idle { get; init; }
    }

    #region Native Interop

    [DllImport("libc")]
    private static extern int sysctl(int[] name, uint namelen, IntPtr oldp, ref int oldlenp, IntPtr newp, uint newlen);

    [DllImport("libc")]
    private static extern int sysctlbyname(string name, IntPtr oldp, ref int oldlenp, IntPtr newp, uint newlen);

    private const int CTL_HW = 6;
    private const int HW_CPUSTATS = 17;

    [StructLayout(LayoutKind.Sequential)]
    private struct CpuStats
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public CpuTimeInfo[] cpu_time;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CpuTimeInfo
    {
        public long cp_user;
        public long cp_sys;
        public long cp_idle;
        public long cp_nice;
    }

    #endregion
}
