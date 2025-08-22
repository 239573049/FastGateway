using System.Runtime.CompilerServices;

namespace TunnelClient.Monitor;

public class ServerConnection : IAsyncDisposable
{
    private static readonly string Ping = "PING";
    private static readonly string Pong = "PONG";
    private static readonly ReadOnlyMemory<byte> PingLine = "PING\r\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> PongLine = "PONG\r\n"u8.ToArray();
    private readonly TimeSpan _keepAliveTimeout;
    private readonly Timer? _keepAliveTimer;

    private readonly ILogger<ServerConnection> _logger;
    private readonly Stream _stream;

    public ServerConnection(Stream stream, TimeSpan keepAliveInterval, ILogger<ServerConnection> logger)
    {
        _stream = stream;
        _logger = logger;

        if (keepAliveInterval > TimeSpan.Zero)
        {
            _keepAliveTimeout = keepAliveInterval.Add(TimeSpan.FromSeconds(10d));
            _keepAliveTimer = new Timer(KeepAliveTimerTick, null, keepAliveInterval, keepAliveInterval);
        }
        else
        {
            _keepAliveTimeout = Timeout.InfiniteTimeSpan;
        }
    }

    public ValueTask DisposeAsync()
    {
        _keepAliveTimer?.Dispose();
        return _stream.DisposeAsync();
    }

    /// <summary>
    ///     心跳timer
    /// </summary>
    /// <param name="state"></param>
    private async void KeepAliveTimerTick(object? state)
    {
        try
        {
            Log.LogSendPing(_logger);
            await _stream.WriteAsync(PingLine);
        }
        catch (Exception)
        {
            if (_keepAliveTimer != null) await _keepAliveTimer.DisposeAsync();
        }
    }

    public async IAsyncEnumerable<Guid> ReadTunnelIdAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var textReader = new StreamReader(_stream, leaveOpen: true);
        while (!cancellationToken.IsCancellationRequested)
        {
            var textTask = textReader.ReadLineAsync(cancellationToken);
            var text = _keepAliveTimeout <= TimeSpan.Zero
                ? await textTask
                : await textTask.AsTask().WaitAsync(_keepAliveTimeout, cancellationToken);

            if (text == null) yield break;

            if (text == Ping)
            {
                Log.LogSendPing(_logger);

                // 服务器发送PING，回复PONG
                await _stream.WriteAsync(PongLine, cancellationToken);
            }
            else if (text == Pong)
            {
                Log.LogRecvPong(_logger);
                // 服务器发送PONG
            }
            else if (Guid.TryParse(text, out var tunnelId))
            {
                Log.LogRecvPing(_logger);
                yield return tunnelId;
            }
            else
            {
                Console.WriteLine($"Unknown news：{text}");
            }
        }
    }
}

internal static partial class Log
{
    [LoggerMessage(LogLevel.Debug, "发出PING请求")]
    public static partial void LogSendPing(ILogger logger);

    [LoggerMessage(LogLevel.Debug, "收到PING请求")]
    public static partial void LogRecvPing(ILogger logger);

    [LoggerMessage(LogLevel.Debug, "收到PONG回应")]
    public static partial void LogRecvPong(ILogger logger);

    [LoggerMessage(LogLevel.Debug, "隧道连接已关闭，隧道ID：{tunnelId}，目标地址：{targetUri}，持续时间：{duration}，当前隧道数量：{tunnelCount}")]
    public static partial void LogTunnelClosed(ILogger logger, Guid tunnelId, string targetUri, double duration,
        int tunnelCount);

    [LoggerMessage(LogLevel.Error, "隧道连接发生错误，隧道ID：{tunnelId}，错误信息：{message}")]
    public static partial void LogTunnelError(ILogger logger, Guid tunnelId, string message);

    [LoggerMessage(LogLevel.Information, "新增隧道连接，隧道ID：{tunnelId}，目标地址：{targetUri}，当前隧道数量：{tunnelCount}")]
    public static partial void LogNewTunnel(ILogger logger, Guid tunnelId, string targetUri, int tunnelCount);
}