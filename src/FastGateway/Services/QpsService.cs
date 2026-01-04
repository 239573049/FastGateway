using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace FastGateway.Services;

public sealed class QpsItem
{
    private int _lastQps; // last calculated 3s average QPS
    private long _totalRequests;
    private long _successRequests;
    private long _failedRequests;
    private readonly ConcurrentQueue<long> _responseTimes = new();

    // 3s window tracking (lock-free)
    private long _windowRequestCount; // requests accumulated in current window
    private long _windowStartTicks = Stopwatch.GetTimestamp();
    
    // 缓存最近 50 次 QPS 数据
    private readonly ConcurrentQueue<QpsHistoryItem> _qpsHistory = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementRequests()
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Increment(ref _windowRequestCount);
        TryCalculateWindowQps();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TryCalculateWindowQps()
    {
        // Read start ticks
        var start = Volatile.Read(ref _windowStartTicks);
        double elapsedSeconds = (Stopwatch.GetTimestamp() - start) / (double)Stopwatch.Frequency;
        if (elapsedSeconds < 3.0) return;

        // Re-evaluate with fresh timestamp
        long nowTicks = Stopwatch.GetTimestamp();
        start = Volatile.Read(ref _windowStartTicks);
        elapsedSeconds = (nowTicks - start) / (double)Stopwatch.Frequency;
        if (elapsedSeconds < 3.0) return;

        // Attempt to rotate window atomically
        if (Interlocked.CompareExchange(ref _windowStartTicks, nowTicks, start) == start)
        {
            long windowCount = Interlocked.Exchange(ref _windowRequestCount, 0);
            var calculatedQps = elapsedSeconds > 0 ? (int)(windowCount / elapsedSeconds) : 0;
            _lastQps = calculatedQps;
            
            // 添加到历史记录
            _qpsHistory.Enqueue(new QpsHistoryItem
            {
                Time = DateTime.Now.ToString("HH:mm:ss"),
                Qps = calculatedQps
            });
            
            // 保持最近 50 条记录
            while (_qpsHistory.Count > 50)
            {
                _qpsHistory.TryDequeue(out _);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementSuccessRequests()
    {
        Interlocked.Increment(ref _successRequests);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementFailedRequests()
    {
        Interlocked.Increment(ref _failedRequests);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void RecordResponseTime(long milliseconds)
    {
        _responseTimes.Enqueue(milliseconds);
        // Keep queue size <= 1000
        while (_responseTimes.Count > 1000)
        {
            _responseTimes.TryDequeue(out _);
        }
    }

    // Manual trigger (e.g., timer) to ensure QPS updates even during idle periods
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CalculateQps()
    {
        TryCalculateWindowQps();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetQps()
    {
        // Ensure we refresh if window elapsed without new requests
        TryCalculateWindowQps();
        return _lastQps;
    }

    public (long total, long success, long failed) GetRequestCounts()
    {
        return (_totalRequests, _successRequests, _failedRequests);
    }

    public ResponseTimeStats GetResponseTimeStats()
    {
        var times = _responseTimes.ToArray();
        if (times.Length == 0)
        {
            return new ResponseTimeStats(0, 0, 0, 0, 0);
        }

        Array.Sort(times);
        var avg = (long)times.Average();
        var min = times[0];
        var max = times[^1];
        var p95Index = (int)(times.Length * 0.95);
        var p99Index = (int)(times.Length * 0.99);
        var p95 = times[Math.Min(p95Index, times.Length - 1)];
        var p99 = times[Math.Min(p99Index, times.Length - 1)];

        return new ResponseTimeStats(avg, p95, p99, min, max);
    }
    
    public QpsHistoryItem[] GetQpsHistory()
    {
        return _qpsHistory.ToArray();
    }
}

public record QpsHistoryItem
{
    public string Time { get; init; } = string.Empty;
    public int Qps { get; init; }
}

public record ResponseTimeStats(long Avg, long P95, long P99, long Min, long Max);

public static class SystemMonitorService
{
    private static readonly Lazy<PerformanceCounter> _cpuCounter = new(() =>
        new PerformanceCounter("Processor", "% Processor Time", "_Total"));
    private static readonly Lazy<PerformanceCounter> _memoryCounter = new(() =>
        new PerformanceCounter("Memory", "Available MBytes"));
    private static readonly Lazy<PerformanceCounter> _diskReadCounter = new(() =>
        new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total"));
    private static readonly Lazy<PerformanceCounter> _diskWriteCounter = new(() =>
        new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total"));

    private static bool _performanceCountersAvailable = true;
    private static bool _diskCountersAvailable = true;

    static SystemMonitorService()
    {
        try
        {
            if (_performanceCountersAvailable)
            {
                _cpuCounter.Value.NextValue();
                _memoryCounter.Value.NextValue();
            }
        }
        catch
        {
            _performanceCountersAvailable = false;
        }

        try
        {
            if (_diskCountersAvailable)
            {
                _diskReadCounter.Value.NextValue();
                _diskWriteCounter.Value.NextValue();
            }
        }
        catch
        {
            _diskCountersAvailable = false;
        }
    }

    public static SystemStats GetSystemStats()
    {
        try
        {
            float cpuUsage = 0;
            float availableMemoryMB = 0;
            long diskReadBytesPerSec = 0;
            long diskWriteBytesPerSec = 0;

            if (_performanceCountersAvailable)
            {
                try
                {
                    cpuUsage = _cpuCounter.Value.NextValue();
                    availableMemoryMB = _memoryCounter.Value.NextValue();
                }
                catch
                {
                    _performanceCountersAvailable = false;
                }
            }

            if (_diskCountersAvailable)
            {
                try
                {
                    diskReadBytesPerSec = (long)_diskReadCounter.Value.NextValue();
                    diskWriteBytesPerSec = (long)_diskWriteCounter.Value.NextValue();
                }
                catch
                {
                    _diskCountersAvailable = false;
                }
            }

            var totalMemoryBytes = GC.GetTotalMemory(false);
            var workingSetBytes = Environment.WorkingSet;

            long totalSystemMemoryBytes;
            long availableSystemMemoryBytes;

            if (!TryGetSystemMemoryBytes(out totalSystemMemoryBytes, out availableSystemMemoryBytes))
            {
                if (_performanceCountersAvailable && availableMemoryMB > 0)
                {
                    availableSystemMemoryBytes = (long)(availableMemoryMB * 1024 * 1024);
                    totalSystemMemoryBytes = availableSystemMemoryBytes + workingSetBytes;
                }
                else
                {
                    totalSystemMemoryBytes = Math.Max(workingSetBytes * 4, workingSetBytes);
                    availableSystemMemoryBytes = Math.Max(0, totalSystemMemoryBytes - workingSetBytes);
                }
            }

            var usedSystemMemoryBytes = Math.Max(0, totalSystemMemoryBytes - availableSystemMemoryBytes);
            var memoryUsagePercent = totalSystemMemoryBytes > 0
                ? Math.Min((double)usedSystemMemoryBytes / totalSystemMemoryBytes * 100, 100)
                : 0;

            return new SystemStats(
                CpuUsage: cpuUsage,
                MemoryUsagePercent: memoryUsagePercent,
                TotalMemoryBytes: totalSystemMemoryBytes,
                UsedMemoryBytes: usedSystemMemoryBytes,
                AvailableMemoryBytes: availableSystemMemoryBytes,
                ProcessorCount: Environment.ProcessorCount,
                WorkingSetBytes: workingSetBytes,
                GCMemoryBytes: totalMemoryBytes,
                DiskReadBytesPerSec: diskReadBytesPerSec,
                DiskWriteBytesPerSec: diskWriteBytesPerSec
            );
        }
        catch
        {
            var workingSetBytes = Environment.WorkingSet;
            var gcMemoryBytes = GC.GetTotalMemory(false);

            return new SystemStats(
                CpuUsage: 0,
                MemoryUsagePercent: 0,
                TotalMemoryBytes: workingSetBytes,
                UsedMemoryBytes: workingSetBytes,
                AvailableMemoryBytes: 0,
                ProcessorCount: Environment.ProcessorCount,
                WorkingSetBytes: workingSetBytes,
                GCMemoryBytes: gcMemoryBytes,
                DiskReadBytesPerSec: 0,
                DiskWriteBytesPerSec: 0
            );
        }
    }

    private static bool TryGetSystemMemoryBytes(out long totalBytes, out long availableBytes)
    {
        totalBytes = 0;
        availableBytes = 0;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return TryGetWindowsMemoryBytes(out totalBytes, out availableBytes);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return TryGetLinuxMemoryBytes(out totalBytes, out availableBytes);
        }

        return false;
    }

    private static bool TryGetWindowsMemoryBytes(out long totalBytes, out long availableBytes)
    {
        totalBytes = 0;
        availableBytes = 0;

        try
        {
            var memoryInfo = new MEMORY_INFO();
            memoryInfo.DWLength = (uint)Marshal.SizeOf(memoryInfo);
            if (!GlobalMemoryStatusEx(ref memoryInfo))
            {
                return false;
            }

            totalBytes = (long)memoryInfo.ullTotalPhys;
            availableBytes = (long)memoryInfo.ullAvailPhys;
            return totalBytes > 0 && availableBytes >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryGetLinuxMemoryBytes(out long totalBytes, out long availableBytes)
    {
        totalBytes = 0;
        availableBytes = 0;

        try
        {
            if (!File.Exists("/proc/meminfo"))
            {
                return false;
            }

            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                {
                    totalBytes = ParseMeminfoLineToBytes(line);
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                {
                    availableBytes = ParseMeminfoLineToBytes(line);
                }

                if (totalBytes > 0 && availableBytes > 0)
                {
                    break;
                }
            }

            return totalBytes > 0 && availableBytes > 0;
        }
        catch
        {
            return false;
        }
    }

    private static long ParseMeminfoLineToBytes(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return 0;
        }

        return long.TryParse(parts[1], out var kb) ? kb * 1024 : 0;
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORY_INFO mi);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_INFO
    {
        public uint DWLength;
        public uint DWMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPagefile;
        public ulong ullAvailPagefile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}

public record SystemStats(
    float CpuUsage,
    double MemoryUsagePercent,
    long TotalMemoryBytes,
    long UsedMemoryBytes,
    long AvailableMemoryBytes,
    int ProcessorCount,
    long WorkingSetBytes,
    long GCMemoryBytes,
    long DiskReadBytesPerSec,
    long DiskWriteBytesPerSec
);

public static class QpsService
{
    private static readonly QpsItem QpsItem = new();
    private static readonly DateTime _startTime = DateTime.UtcNow;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IncrementServiceRequests()
    {
        QpsItem.IncrementRequests();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordSuccessRequest(long responseTimeMs)
    {
        QpsItem.IncrementSuccessRequests();
        QpsItem.RecordResponseTime(responseTimeMs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordFailedRequest(long responseTimeMs = 0)
    {
        QpsItem.IncrementFailedRequests();
        if (responseTimeMs > 0)
        {
            QpsItem.RecordResponseTime(responseTimeMs);
        }
    }

    public static void CalculateServiceQps()
    {
        QpsItem.CalculateQps();
    }

    public static int GetServiceQps()
    {
        return QpsItem.GetQps();
    }

    public static (long total, long success, long failed) GetRequestCounts()
    {
        return QpsItem.GetRequestCounts();
    }

    public static ResponseTimeStats GetResponseTimeStats()
    {
        return QpsItem.GetResponseTimeStats();
    }

    public static QpsHistoryItem[] GetQpsHistory()
    {
        return QpsItem.GetQpsHistory();
    }

    public static TimeSpan GetUptime()
    {
        return DateTime.UtcNow - _startTime;
    }
}

public static class ApiQpsService
{
    public static IEndpointRouteBuilder MapApiQpsService(this IEndpointRouteBuilder app)
    {
        var qpsService = app.MapGroup("/api/v1/qps")
            .WithTags("QPS")
            .WithDescription("QPS管理")
            .RequireAuthorization()
            .WithDisplayName("QPS");

        qpsService.MapGet(string.Empty, GetAsync)
            .WithDescription("获取QPS")
            .WithDisplayName("获取QPS")
            .WithTags("QPS");

        return app;
    }

    public static async Task GetAsync(HttpContext context)
    {
        try
        {
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            long initialBytesSent = 0;
            long initialBytesReceived = 0;

            foreach (var ni in networkInterfaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    !ni.Supports(NetworkInterfaceComponent.IPv4)) continue;
                try
                {
                    var interfaceStats = ni.GetIPv4Statistics();
                    initialBytesSent += interfaceStats.BytesSent;
                    initialBytesReceived += interfaceStats.BytesReceived;
                }
                catch { }
            }

            await Task.Delay(1000);

            long bytesSentAfter1Sec = 0;
            long bytesReceivedAfter1Sec = 0;

            foreach (var ni in networkInterfaces)
            {
                if (ni.OperationalStatus != OperationalStatus.Up ||
                    !ni.Supports(NetworkInterfaceComponent.IPv4)) continue;
                try
                {
                    var interfaceStats = ni.GetIPv4Statistics();
                    bytesSentAfter1Sec += interfaceStats.BytesSent;
                    bytesReceivedAfter1Sec += interfaceStats.BytesReceived;
                }
                catch { }
            }

            var upload = Math.Max(0, bytesSentAfter1Sec - initialBytesSent);
            var download = Math.Max(0, bytesReceivedAfter1Sec - initialBytesReceived);

            var systemStats = SystemMonitorService.GetSystemStats();
            var requestCounts = QpsService.GetRequestCounts();
            var responseTimeStats = QpsService.GetResponseTimeStats();
            var qpsHistory = QpsService.GetQpsHistory();
            var uptime = QpsService.GetUptime();

            var data = new
            {
                qps = QpsService.GetServiceQps(), // 3s average QPS
                qpsHistory = qpsHistory.Select(h => new { time = h.Time, qps = h.Qps }).ToArray(),
                now = DateTime.Now.ToString("HH:mm:ss"),
                upload,
                download,
                requests = new
                {
                    total = requestCounts.total,
                    success = requestCounts.success,
                    failed = requestCounts.failed,
                    successRate = requestCounts.total > 0 ? Math.Round((double)requestCounts.success / requestCounts.total * 100, 2) : 0.0
                },
                responseTime = new
                {
                    avg = responseTimeStats.Avg,
                    p95 = responseTimeStats.P95,
                    p99 = responseTimeStats.P99,
                    min = responseTimeStats.Min,
                    max = responseTimeStats.Max
                },
                system = new
                {
                    cpu = new
                    {
                        usage = Math.Round(systemStats.CpuUsage, 2),
                        cores = systemStats.ProcessorCount
                    },
                    memory = new
                    {
                        usage = Math.Round(systemStats.MemoryUsagePercent, 2),
                        total = systemStats.TotalMemoryBytes,
                        used = systemStats.UsedMemoryBytes,
                        available = systemStats.AvailableMemoryBytes,
                        workingSet = systemStats.WorkingSetBytes,
                        gc = systemStats.GCMemoryBytes
                    },
                    disk = new
                    {
                        readBytesPerSec = systemStats.DiskReadBytesPerSec,
                        writeBytesPerSec = systemStats.DiskWriteBytesPerSec
                    }
                },
                service = new
                {
                    isOnline = true,
                    uptime = new
                    {
                        days = uptime.Days,
                        hours = uptime.Hours,
                        minutes = uptime.Minutes,
                        seconds = uptime.Seconds,
                        totalSeconds = (long)uptime.TotalSeconds
                    },
                    lastUpdate = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
                }
            };

            await context.Response.WriteAsJsonAsync(data);
        }
        finally { }
    }
}
