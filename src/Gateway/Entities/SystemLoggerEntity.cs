namespace Gateway.Entities;

public sealed class SystemLoggerEntity : Entity
{
    /// <summary>
    /// 请求数量
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// 异常数量
    /// </summary>
    public int ErrorRequestCount { get; set; }

    /// <summary>
    /// 当前时间
    /// </summary>
    public DateTime CurrentTime { get; set; }
    
    /// <summary>
    /// 获取读取速率
    /// </summary>
    public double ReadRate { get; init; }

    /// <summary>
    /// 获取写入速率
    /// </summary>
    public double WriteRate { get; init; }
}