using System.Net.NetworkInformation;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FastGateway.Service.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace FastGateway.Service.Services;

public sealed class QpsItem
{
    private int _lastQps;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementRequests()
    {
        Interlocked.Increment(ref _lastQps);
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
}

public static class QpsService
{
    private static readonly QpsItem qpsItem = new();
    private static bool _enable;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void IncrementServiceRequests()
    {
        if (_enable == false)
        {
            return;
        }

        qpsItem.IncrementRequests();
    }

    public static void CalculateServiceQps()
    {
        if (_enable == false)
        {
            return;
        }

        qpsItem.CalculateQps();
    }

    public static int GetServiceQps()
    {
        var qps = qpsItem.GetQps();

        qpsItem.CalculateQps();

        return qps;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void EnableQps(bool enable)
    {
        _enable = enable;
    }
}

public static class ApiQpsService
{
    public static WebApplication UseApiQpsService(this WebApplication app)
    {
        var qpsService = app.MapGroup("/api/v1/qps")
            .WithTags("QPS")
            .WithDescription("QPS管理")
            .WithDisplayName("QPS");

        qpsService.MapGet(string.Empty, GetAsync)
            .WithDescription("获取QPS")
            .WithDisplayName("获取QPS")
            .WithTags("QPS");

        return app;
    }

    public static async Task GetAsync(HttpContext context)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        QpsService.EnableQps(true);

        for (var i = 0; i < 10; i++)
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
                var interfaceStats = ni.GetIPv4Statistics();
                initialBytesSent += interfaceStats.BytesSent;
                initialBytesReceived += interfaceStats.BytesReceived;
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
                var interfaceStats = ni.GetIPv4Statistics();
                bytesSentAfter1Sec += interfaceStats.BytesSent;
                bytesReceivedAfter1Sec += interfaceStats.BytesReceived;
            }

            // 计算1秒内发送和接收的总字节
            var upload = bytesSentAfter1Sec - initialBytesSent;
            var download = bytesReceivedAfter1Sec - initialBytesReceived;

            await context.Response.WriteAsync($"data:{JsonSerializer.Serialize(new
            {
                qps = QpsService.GetServiceQps(),
                now = DateTime.Now.ToString("HH:mm:ss"),
                upload,
                download
            })}\n\n");

            await context.Response.Body.FlushAsync();
        }

        QpsService.EnableQps(false);
    }
}