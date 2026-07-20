using System.Threading.Channels;

namespace FastGateway.Services.Statistics;

/// <summary>
///     拦截原因（写入 HttpContext.Items 供采集中间件读取）
/// </summary>
public enum BlockReason : byte
{
    None = 0,
    Blacklist = 1,
    RateLimit = 2,
    Whitelist = 3
}

/// <summary>
///     单次请求的统计采集条目（热路径只填充该结构，富化在后台消费者完成）
/// </summary>
public readonly struct RequestStatEntry
{
    public required long Ts { get; init; }
    public required string ServerId { get; init; }
    public required string Host { get; init; }
    public required string Path { get; init; }
    public required string Method { get; init; }
    public required int Status { get; init; }
    public required int ElapsedMs { get; init; }
    public required string Ip { get; init; }
    public required string? UserAgent { get; init; }
    public required string? Referer { get; init; }
    public required byte Blocked { get; init; }
    public required bool IsPage { get; init; }
}

/// <summary>
///     全局静态采集通道。必须为静态：每个网关 Server 是独立 DI 容器的 WebApplication，
///     与 QpsService/AbnormalIpMonitor 同模式；唯一消费者为 StatisticsBackgroundService。
/// </summary>
public static class StatisticsCollector
{
    public const string BlockReasonKey = "FastGateway.BlockReason";

    private static readonly Channel<RequestStatEntry> Channel =
        System.Threading.Channels.Channel.CreateBounded<RequestStatEntry>(
            new BoundedChannelOptions(100_000)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropWrite
            });

    private static long _dropped;

    public static ChannelReader<RequestStatEntry> Reader => Channel.Reader;

    /// <summary>
    ///     采集积压丢弃总数（通道满载时不阻塞请求线程，直接丢弃并计数）
    /// </summary>
    public static long DroppedCount => Volatile.Read(ref _dropped);

    public static void Enqueue(in RequestStatEntry entry)
    {
        if (!Channel.Writer.TryWrite(entry))
            Interlocked.Increment(ref _dropped);
    }
}
