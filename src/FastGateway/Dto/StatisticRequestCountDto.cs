namespace FastGateway.Dto;

public sealed class StatisticRequestCountDto
{
    /// <summary>
    /// 请求总数
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// 异常4xx数量
    /// </summary>
    public int Error4xxCount { get; set; }

    /// <summary>
    /// 异常5xx数量
    /// </summary>
    public int Error5xxCount { get; set; }

    /// <summary>
    /// 关联服务ID
    /// </summary>
    public string ServiceId { get; set; }

    public DateTime CreatedTime { get; set; }
}