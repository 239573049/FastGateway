namespace FastGateway.Entities;

public sealed class RequestSourceEntity : Entity
{
    /// <summary>
    /// 访问IP
    /// </summary>
    [Column(IsPrimary = true, IsIdentity = false)]
    public string Ip { get; set; }
    
    /// <summary>
    /// 请求域名
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// 归属地
    /// </summary>
    public string HomeAddress { get; set; }

    /// <summary>
    /// 请求数量
    /// </summary>
    public long RequestCount { get; set; }

    /// <summary>
    /// 最新请求时间
    /// </summary>
    public DateTime LastRequestTime { get; set; }

    /// <summary>
    /// 请求平台
    /// </summary>
    public string Platform { get; set; }
}