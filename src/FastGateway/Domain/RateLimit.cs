using System.Threading.RateLimiting;

namespace FastGateway.Domain;

public sealed class RateLimit
{
    public RateLimitType Type { get; set; }

    #region 固定窗口限制器

    public int? PermitLimit { get; set; }

    /// <summary>
    /// 限流时间窗口
    /// </summary>
    public TimeSpan? Window { get; set; }

    /// <summary>
    /// QueueProcessingOrder
    /// </summary>
    public QueueProcessingOrder? QueueProcessingOrder { get; set; }

    /// <summary>
    /// QueueLimit
    /// </summary>
    public int? QueueLimit { get; set; }

    #endregion

    #region 滑动窗口限制器

    /// <summary>
    /// SegmentsPerWindow 
    /// </summary>
    public int? SegmentsPerWindow { get; set; }

    #endregion

    #region 令牌桶限制器

    /// <summary>
    /// TokenLimit
    /// </summary>
    public int? TokenLimit { get; set; }

    /// <summary>
    /// TokensPerPeriod
    /// </summary>
    public int? TokensPerPeriod { get; set; }

    /// <summary>
    /// AutoReplenishment
    /// </summary>
    public bool? AutoReplenishment { get; set; }

    #endregion

    /// <summary>
    /// 拒绝响应状态码
    /// </summary>
    public int RejectionStatusCode { get; set; }
}