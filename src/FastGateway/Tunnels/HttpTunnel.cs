using Core;

namespace FastGateway.Tunnels;

/// <summary>
///     http隧道
/// </summary>
public sealed partial class HttpTunnel(Stream inner, Guid tunnelId, TransportProtocol protocol, ILogger logger)
    : DelegatingStream(inner)
{
    private readonly TaskCompletionSource _closeTaskCompletionSource = new();
    private readonly long _tickCout = Environment.TickCount64;
    private AgentClientConnection? _connection;

    /// <summary>
    ///     等待HttpClient对其关闭
    /// </summary>
    public Task Closed => _closeTaskCompletionSource.Task;

    /// <summary>
    ///     隧道标识
    /// </summary>
    public Guid Id { get; } = tunnelId;

    /// <summary>
    ///     传输协议
    /// </summary>
    public TransportProtocol Protocol { get; } = protocol;

    public void BindConnection(AgentClientConnection connection)
    {
        _connection = connection;
    }

    public override ValueTask DisposeAsync()
    {
        SetClosedResult();
        return Inner.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        SetClosedResult();
        Inner.Dispose();
    }

    private void SetClosedResult()
    {
        if (_closeTaskCompletionSource.TrySetResult())
        {
            var httpTunnelCount = _connection?.DecrementHttpTunnelCount();
            var lifeTime = TimeSpan.FromMilliseconds(Environment.TickCount64 - _tickCout);
            Log.LogTunnelClosed(logger, _connection?.ClientId, Protocol, Id, lifeTime,
                httpTunnelCount);
        }
    }

    public override string ToString()
    {
        return Id.ToString();
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Information,
            "[{clientId}] 关闭了{protocol}协议隧道{tunnelId}，生命周期为{lifeTime}，其当前隧道总数为{tunnelCount}")]
        public static partial void LogTunnelClosed(ILogger logger, string? clientId, TransportProtocol protocol,
            Guid tunnelId, TimeSpan lifeTime, int? tunnelCount);
    }
}