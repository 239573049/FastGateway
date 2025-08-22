using System.Collections.Concurrent;

namespace FastGateway.Tunnels;

public sealed partial class HttpTunnelFactory(ILogger<HttpTunnel> logger)
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<HttpTunnel>> httpTunnelCompletionSources =
        new();

    public bool Contains(Guid tunnelId)
    {
        return httpTunnelCompletionSources.ContainsKey(tunnelId);
    }

    public bool SetResult(HttpTunnel httpTunnel)
    {
        return httpTunnelCompletionSources.TryRemove(httpTunnel.Id, out var source) &&
               source.TrySetResult(httpTunnel);
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information, "[{clientId}] 请求创建隧道{tunnelId}")]
        public static partial void LogTunnelCreating(ILogger logger, string clientId, Guid tunnelId);

        [LoggerMessage(LogLevel.Warning, "[{clientId}] 创建隧道{tunnelId}失败：{reason}")]
        public static partial void LogTunnelCreateFailure(ILogger logger, string clientId, Guid tunnelId,
            string? reason);

        [LoggerMessage(LogLevel.Information,
            "[{clientId}] 创建了{protocol}协议隧道{tunnelId}，过程耗时{elapsed}，其当前隧道总数为{tunnelCount}")]
        public static partial void LogTunnelCreateSuccess(ILogger logger, string clientId, TransportProtocol protocol,
            Guid tunnelId, TimeSpan elapsed, int tunnelCount);
    }
}