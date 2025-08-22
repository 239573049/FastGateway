using System.Buffers;
using System.Text;

namespace FastGateway.Tunnels;

public sealed partial class AgentClientConnection : IAsyncDisposable
{
    private const int BufferSize = 8;
    private const string Ping = "PING";
    private const string Pong = "PONG";
    private static readonly ReadOnlyMemory<byte> PingLine = "PING\r\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> PongLine = "PONG\r\n"u8.ToArray();
    private readonly CancellationTokenSource _disposeTokenSource = new();
    private readonly TimeSpan _keepAliveTimeout;
    private readonly Timer? _keepAliveTimer;
    private readonly ILogger _logger;
    private readonly Stream _stream;
    private int _httpTunnelCount;

    public AgentClientConnection(string clientId, Stream stream, ConnectionConfig config, ILogger logger)
    {
        ClientId = clientId;
        _stream = stream;
        _logger = logger;

        var keepAliveInterval = config.KeepAliveInterval;
        if (config.KeepAlive && keepAliveInterval > TimeSpan.Zero)
        {
            _keepAliveTimeout = keepAliveInterval.Add(TimeSpan.FromSeconds(10d));
            _keepAliveTimer = new Timer(KeepAliveTimerTick, null, keepAliveInterval, keepAliveInterval);
        }
        else
        {
            _keepAliveTimeout = Timeout.InfiniteTimeSpan;
        }
    }

    public string ClientId { get; }

    public int HttpTunnelCount => _httpTunnelCount;

    public async ValueTask DisposeAsync()
    {
        if (!_disposeTokenSource.IsCancellationRequested)
        {
            _disposeTokenSource.Cancel();
            _disposeTokenSource.Dispose();

            await _stream.DisposeAsync();
        }
    }

    /// <summary>
    ///     心跳timer
    /// </summary>
    /// <param name="state"></param>
    private async void KeepAliveTimerTick(object? state)
    {
        try
        {
            await _stream.WriteAsync(PingLine);
            Log.LogSendPing(_logger, ClientId);
        }
        catch (Exception)
        {
            _keepAliveTimer?.Dispose();
        }
    }

    public async Task CreateHttpTunnelAsync(Guid tunnelId,
        CancellationToken cancellationToken)
    {
        const int size = 64;
        var tunnelIdLine = $"{tunnelId}\r\n";

        using var owner = MemoryPool<byte>.Shared.Rent(size);
        var length = Encoding.ASCII.GetBytes(tunnelIdLine, owner.Memory.Span);

        var buffer = owner.Memory[..length];
        await _stream.WriteAsync(buffer, cancellationToken);
    }

    public int IncrementHttpTunnelCount()
    {
        return Interlocked.Increment(ref _httpTunnelCount);
    }

    public int DecrementHttpTunnelCount()
    {
        return Interlocked.Decrement(ref _httpTunnelCount);
    }

    public async Task WaitForCloseAsync()
    {
        try
        {
            var cancellationToken = _disposeTokenSource.Token;
            await HandleConnectionAsync(cancellationToken);
        }
        catch (Exception)
        {
        }
    }

    private async Task HandleConnectionAsync(CancellationToken cancellationToken)
    {
        using var textReader = new StreamReader(_stream, bufferSize: BufferSize, leaveOpen: true);
        while (!cancellationToken.IsCancellationRequested)
        {
            var textTask = textReader.ReadLineAsync(cancellationToken);
            var text = _keepAliveTimeout <= TimeSpan.Zero
                ? await textTask
                : await textTask.AsTask().WaitAsync(_keepAliveTimeout, cancellationToken);

            switch (text)
            {
                case null:
                    break;

                case Ping:
                    Log.LogRecvPing(_logger, ClientId);
                    await _stream.WriteAsync(PongLine, cancellationToken);
                    break;

                case Pong:
                    Log.LogRecvPong(_logger, ClientId);
                    break;

                default:
                    Log.LogRecvUnknown(_logger, ClientId, text);
                    break;
            }
        }
    }

    private static partial class Log
    {
        [LoggerMessage(LogLevel.Debug, "[{clientId}] 发出PING请求")]
        public static partial void LogSendPing(ILogger logger, string clientId);

        [LoggerMessage(LogLevel.Debug, "[{clientId}] 收到PING请求")]
        public static partial void LogRecvPing(ILogger logger, string clientId);

        [LoggerMessage(LogLevel.Debug, "[{clientId}] 收到PONG回应")]
        public static partial void LogRecvPong(ILogger logger, string clientId);

        [LoggerMessage(LogLevel.Debug, "[{clientId}] 收到未知数据: {text}")]
        public static partial void LogRecvUnknown(ILogger logger, string clientId, string text);

        [LoggerMessage(LogLevel.Debug, "[{clientId}] 连接已关闭")]
        public static partial void LogClosed(ILogger logger, string clientId);
    }
}