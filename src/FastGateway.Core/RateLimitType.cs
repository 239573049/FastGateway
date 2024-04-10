namespace FastGateway.Core;

public enum RateLimitType : byte
{
    /// <summary>
    /// 并发策略
    /// </summary>
    Concurrent = 1,
    
    /// <summary>
    /// 令牌桶
    /// </summary>
    TokenBucket = 2,
    
    /// <summary>
    /// 滑动窗口
    /// </summary>
    SlidingWindow = 3,
    
    /// <summary>
    /// 固定窗口
    /// </summary>
    FixedWindow = 4
}