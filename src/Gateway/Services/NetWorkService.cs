namespace Gateway.Services;

public class NetWorkService
{
    public static async Task StreamAsync(HttpContext context)
    {
        // 使用sse，返回响应头
        context.Response.Headers.ContentType = "text/event-stream";

        int i = 0;

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
                if (ni.OperationalStatus == OperationalStatus.Up && ni.Supports(NetworkInterfaceComponent.IPv4))
                {
                    var interfaceStats = ni.GetIPv4Statistics();
                    initialBytesSent += interfaceStats.BytesSent;
                    initialBytesReceived += interfaceStats.BytesReceived;
                }
            }

            // 等待1秒钟
            await Task.Delay(1000, context.RequestAborted);

            // 存储每个接口1秒后的值
            long bytesSentAfter1Sec = 0;
            long bytesReceivedAfter1Sec = 0;

            // 再次遍历网络接口
            foreach (var ni in networkInterfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up && ni.Supports(NetworkInterfaceComponent.IPv4))
                {
                    var interfaceStats = ni.GetIPv4Statistics();
                    bytesSentAfter1Sec += interfaceStats.BytesSent;
                    bytesReceivedAfter1Sec += interfaceStats.BytesReceived;
                }
            }

            // 计算1秒内发送和接收的总字节
            var totalBytesSentIn1Sec = bytesSentAfter1Sec - initialBytesSent;
            var totalBytesReceivedIn1Sec = bytesReceivedAfter1Sec - initialBytesReceived;

            var data =
                $"data:{JsonSerializer.Serialize(new NetWorkDto(totalBytesReceivedIn1Sec, totalBytesSentIn1Sec))}\n\n";

            // 将数据写入到响应流中
            await context.Response.WriteAsync(data, context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);

            i++;

            // 只维持10秒的连接
            if (i > 5)
            {
                break;
            }
        }
    }
}

public static class NetWorkExtension
{
    public static void MapNetWork(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gateway/network", async (NetWorkService netWorkService, HttpContext context) =>
            await NetWorkService.StreamAsync(context));
    }
}