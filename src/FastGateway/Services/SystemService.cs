using FastGateway.Middlewares.FlowAnalytics;

namespace FastGateway.Services;

/// <summary>
///     系统服务
/// </summary>
public static class SystemService
{
    public static async Task StreamAsync(HttpContext context, IFreeSql sql, IFlowAnalyzer flowAnalyzer)
    {
        // 使用sse，返回响应头
        context.Response.Headers.ContentType = "text/event-stream";

        var i = 0;

        var totalErrorCount = (double)await sql.Select<SystemLoggerEntity>().SumAsync(x => x.ErrorRequestCount);
        var totalRequestCount = (double)await sql.Select<SystemLoggerEntity>().SumAsync(x => x.RequestCount);
        var totalRead = (double)await sql.Select<SystemLoggerEntity>().SumAsync(x => x.ReadRate);
        var totalWrite = (double)await sql.Select<SystemLoggerEntity>().SumAsync(x => x.WriteRate);

        while (!context.RequestAborted.IsCancellationRequested)
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

            // 等待1秒钟
            await Task.Delay(1000, context.RequestAborted);

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
            var totalBytesSentIn1Sec = bytesSentAfter1Sec - initialBytesSent;
            var totalBytesReceivedIn1Sec = bytesReceivedAfter1Sec - initialBytesReceived;

            var flowStatisticsDto = flowAnalyzer.GetFlowStatistics();

            var data =
                $"data:{JsonSerializer.Serialize(new NetWorkDto(totalBytesReceivedIn1Sec, totalBytesSentIn1Sec)
                {
                    TotalErrorCount = totalErrorCount,
                    TotalRequestCount = totalRequestCount,
                    ReadRate = flowStatisticsDto.TotalRead,
                    WriteRate = flowStatisticsDto.TotalWrite,
                    TotalWrite = totalRead,
                    TotalRead = totalWrite
                })}\n\n";

            // 将数据写入到响应流中
            await context.Response.WriteAsync(data, context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);

            i++;

            // 只维持10秒的连接
            if (i > 10) break;
        }
    }
}

public static class SystemExtension
{
    public static void MapSystem(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gateway/system", async (HttpContext context, IFreeSql sql, IFlowAnalyzer flowAnalyzer) =>
            await SystemService.StreamAsync(context, sql, flowAnalyzer))
            .RequireAuthorization();
    }
}