namespace FastGateway.Risk;

/// <summary>
///     登录失败渐进延迟：窗口内失败达到阈值后，每多失败一次延迟翻倍，有上限
/// </summary>
public static class LoginFailureDelay
{
    public const int Threshold = 5;

    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(3);

    // 延迟期间连接一直被占用，限制同时延迟的请求数（管理后台与所有网关共用），超出时直接返回，避免反过来拖垮自己
    private const int MaxConcurrentDelays = 200;
    private static readonly SemaphoreSlim DelaySlots = new(MaxConcurrentDelays, MaxConcurrentDelays);

    public static TimeSpan For(int recentFailures)
    {
        if (recentFailures < Threshold) return TimeSpan.Zero;

        var exponent = Math.Min(recentFailures - Threshold, 10);
        var delay = BaseDelay * (1 << exponent);
        return delay < MaxDelay ? delay : MaxDelay;
    }

    public static async Task DelayAsync(TimeSpan delay, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero || !DelaySlots.Wait(0)) return;

        try
        {
            await Task.Delay(delay, timeProvider, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // 客户端已断开，响应不会再送达
        }
        finally
        {
            DelaySlots.Release();
        }
    }
}
