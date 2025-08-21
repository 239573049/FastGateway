using System.Buffers;
using System.Text;
using System.Text.Json;

namespace FastGateway.Service.Tunnels;

public sealed partial class AgentClientConnection : IAsyncDisposable
{
    private int _httpTunnelCount = 0;
    private readonly Stream _stream;
    private readonly ILogger _logger;
    private readonly Timer? _keepAliveTimer;
    private readonly TimeSpan _keepAliveTimeout;
    private readonly CancellationTokenSource _disposeTokenSource = new();

    private const int BufferSize = 8;
    private const string Ping = "PING";
    private const string Pong = "PONG";
    private static readonly ReadOnlyMemory<byte> PingLine = "PING\r\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> PongLine = "PONG\r\n"u8.ToArray();

    public string ClientId { get; }

    public int HttpTunnelCount => this._httpTunnelCount;

    public AgentClientConnection(string clientId, Stream stream, ConnectionConfig config, ILogger logger)
    {
        this.ClientId = clientId;
        this._stream = stream;
        this._logger = logger;

        var keepAliveInterval = config.KeepAliveInterval;
        if (config.KeepAlive && keepAliveInterval > TimeSpan.Zero)
        {
            this._keepAliveTimeout = keepAliveInterval.Add(TimeSpan.FromSeconds(10d));
            this._keepAliveTimer = new Timer(this.KeepAliveTimerTick, null, keepAliveInterval, keepAliveInterval);
        }
        else
        {
            this._keepAliveTimeout = Timeout.InfiniteTimeSpan;
        }
    }

    /// <summary>
    /// 心跳timer
    /// </summary>
    /// <param name="state"></param>
    private async void KeepAliveTimerTick(object? state)
    {
        try
        {
            await this._stream.WriteAsync(PingLine);
            Log.LogSendPing(this._logger, this.ClientId);
        }
        catch (Exception)
        {
            this._keepAliveTimer?.Dispose();
        }
    }

    public async Task CreateHttpTunnelAsync(Guid tunnelId, CancellationToken cancellationToken)
    {
        const int size = 64;
        var tunnelIdLine = $"{tunnelId}\r\n";

        using var owner = MemoryPool<byte>.Shared.Rent(size);
        var length = Encoding.ASCII.GetBytes(tunnelIdLine, owner.Memory.Span);

        var buffer = owner.Memory[..length];
        await this._stream.WriteAsync(buffer, cancellationToken);
    }

    public int IncrementHttpTunnelCount()
    {
        return Interlocked.Increment(ref this._httpTunnelCount);
    }

    public int DecrementHttpTunnelCount()
    {
        return Interlocked.Decrement(ref this._httpTunnelCount);
    }

    public async Task WaitForCloseAsync()
    {
        try
        {
            var cancellationToken = this._disposeTokenSource.Token;
            await this.HandleConnectionAsync(cancellationToken);
        }
        catch (Exception)
        {
        }
    }

    private async Task HandleConnectionAsync(CancellationToken cancellationToken)
    {
        using var textReader = new StreamReader(this._stream, bufferSize: BufferSize, leaveOpen: true);
        while (cancellationToken.IsCancellationRequested == false)
        {
            var textTask = textReader.ReadLineAsync(cancellationToken);
            var text = this._keepAliveTimeout <= TimeSpan.Zero
                ? await textTask
                : await textTask.AsTask().WaitAsync(this._keepAliveTimeout, cancellationToken);

            switch (text)
            {
                case null:
                    break;

                case Ping:
                    Log.LogRecvPing(this._logger, this.ClientId);
                    await this._stream.WriteAsync(PongLine, cancellationToken);
                    break;

                case Pong:
                    Log.LogRecvPong(this._logger, this.ClientId);
                    break;

                default:
                    Log.LogRecvUnknown(this._logger, this.ClientId, text);
                    break;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (this._disposeTokenSource.IsCancellationRequested == false)
        {
            this._disposeTokenSource.Cancel();
            this._disposeTokenSource.Dispose();

            await this._stream.DisposeAsync();
        }
    }

    static partial class Log
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