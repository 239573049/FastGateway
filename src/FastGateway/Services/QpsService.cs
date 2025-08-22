using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Diagnostics;
using System.Collections.Concurrent;

namespace FastGateway.Services;

public sealed class QpsItem
{
    private int _lastQps;
    private long _totalRequests;
    private long _successRequests;
    private long _failedRequests;
    private readonly ConcurrentQueue<long> _responseTimes = new();
    private readonly object _lockObject = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementRequests()
    {
        Interlocked.Increment(ref _lastQps);
        Interlocked.Increment(ref _totalRequests);
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
        
        // 保持队列大小不超过1000
        while (_responseTimes.Count > 1000)
        {
            _responseTimes.TryDequeue(out _);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void CalculateQps()
    {
        Interlocked.Exchange(ref _lastQps, 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetQps()
    {
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
        // 初始化时读取一次以确保计数器准备就绪
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
            
            // 如果性能计数器不可用，使用近似计算
            if (!_performanceCountersAvailable)
            {
                // 使用进程工作集作为已用内存的粗略估计
                var totalSystemMemoryBytes = workingSetBytes * 4; // 粗略估计系统总内存
                var usedMemoryBytes = workingSetBytes;
                var memoryUsagePercent = (double)usedMemoryBytes / totalSystemMemoryBytes * 100;
                
                return new SystemStats(
                    CpuUsage: 0, // 无法获取CPU使用率
                    MemoryUsagePercent: Math.Min(memoryUsagePercent, 100),
                    TotalMemoryBytes: totalSystemMemoryBytes,
                    UsedMemoryBytes: usedMemoryBytes,
                    AvailableMemoryBytes: totalSystemMemoryBytes - usedMemoryBytes,
                    ProcessorCount: Environment.ProcessorCount,
                    WorkingSetBytes: workingSetBytes,
                    GCMemoryBytes: totalMemoryBytes,
                    DiskReadBytesPerSec: diskReadBytesPerSec,
                    DiskWriteBytesPerSec: diskWriteBytesPerSec
                );
            }
            
            // 使用性能计数器的准确数据
            var totalSystemMemoryBytesAccurate = (long)(availableMemoryMB * 1024 * 1024) + workingSetBytes;
            var usedMemoryBytesAccurate = totalSystemMemoryBytesAccurate - (long)(availableMemoryMB * 1024 * 1024);
            var memoryUsagePercentAccurate = (double)usedMemoryBytesAccurate / totalSystemMemoryBytesAccurate * 100;

            return new SystemStats(
                CpuUsage: cpuUsage,
                MemoryUsagePercent: memoryUsagePercentAccurate,
                TotalMemoryBytes: totalSystemMemoryBytesAccurate,
                UsedMemoryBytes: usedMemoryBytesAccurate,
                AvailableMemoryBytes: (long)(availableMemoryMB * 1024 * 1024),
                ProcessorCount: Environment.ProcessorCount,
                WorkingSetBytes: workingSetBytes,
                GCMemoryBytes: totalMemoryBytes,
                DiskReadBytesPerSec: diskReadBytesPerSec,
                DiskWriteBytesPerSec: diskWriteBytesPerSec
            );
        }
        catch
        {
            // 如果获取系统信息失败，返回基本信息
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
    private static bool _enable;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IncrementServiceRequests()
    {
        if (!_enable) return;
        QpsItem.IncrementRequests();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordSuccessRequest(long responseTimeMs)
    {
        if (!_enable) return;
        QpsItem.IncrementSuccessRequests();
        QpsItem.RecordResponseTime(responseTimeMs);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordFailedRequest(long responseTimeMs = 0)
    {
        if (!_enable) return;
        QpsItem.IncrementFailedRequests();
        if (responseTimeMs > 0)
        {
            QpsItem.RecordResponseTime(responseTimeMs);
        }
    }

    public static void CalculateServiceQps()
    {
        if (!_enable) return;
        QpsItem.CalculateQps();
    }

    public static int GetServiceQps()
    {
        var qps = QpsItem.GetQps();
        QpsItem.CalculateQps();
        return qps;
    }

    public static (long total, long success, long failed) GetRequestCounts()
    {
        return QpsItem.GetRequestCounts();
    }

    public static ResponseTimeStats GetResponseTimeStats()
    {
        return QpsItem.GetResponseTimeStats();
    }

    public static TimeSpan GetUptime()
    {
        return DateTime.UtcNow - _startTime;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnableQps(bool enable)
    {
        _enable = enable;
    }

    public static bool IsEnabled => _enable;
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
        QpsService.EnableQps(true);

        try
        {
            // 获取所有网络接口
            var networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();

            // 存储每个接口的初始值
            long initialBytesSent = 0;
            long initialBytesReceived = 0;

            // 只考虑活动的和支持IPv4的网络接口
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
                catch
                {
                    // 忽略获取统计信息失败的接口
                }
            }

            await Task.Delay(1000);

            // 存储每个接口1秒后的值
            long bytesSentAfter1Sec = 0;
            long bytesReceivedAfter1Sec = 0;

            // 再次遍历网络接口
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
                catch
                {
                    // 忽略获取统计信息失败的接口
                }
            }

            // 计算1秒内发送和接收的总字节
            var upload = Math.Max(0, bytesSentAfter1Sec - initialBytesSent);
            var download = Math.Max(0, bytesReceivedAfter1Sec - initialBytesReceived);

            // 获取系统统计信息
            var systemStats = SystemMonitorService.GetSystemStats();
            var requestCounts = QpsService.GetRequestCounts();
            var responseTimeStats = QpsService.GetResponseTimeStats();
            var uptime = QpsService.GetUptime();

            var data = new
            {
                // 基础QPS数据
                qps = QpsService.GetServiceQps(),
                now = DateTime.Now.ToString("HH:mm:ss"),
                upload,
                download,
                
                // 请求统计
                requests = new
                {
                    total = requestCounts.total,
                    success = requestCounts.success,
                    failed = requestCounts.failed,
                    successRate = requestCounts.total > 0 ? Math.Round((double)requestCounts.success / requestCounts.total * 100, 2) : 0.0
                },
                
                // 响应时间统计
                responseTime = new
                {
                    avg = responseTimeStats.Avg,
                    p95 = responseTimeStats.P95,
                    p99 = responseTimeStats.P99,
                    min = responseTimeStats.Min,
                    max = responseTimeStats.Max
                },
                
                // 系统资源
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
                
                // 服务状态
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
        finally
        {
            QpsService.EnableQps(false);
        }
    }
}