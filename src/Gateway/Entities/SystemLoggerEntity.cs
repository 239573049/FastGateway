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
    
    
}