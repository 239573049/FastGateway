namespace Core.Entities;

/// <summary>
/// 限流策略
/// </summary>
public sealed class RateLimit
{
    public string Id { get; set; }
    
    /// <summary>
    /// 限流策略名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enable { get; set; }
    
    public string Endpoint { get; set; }

    public string Period { get; set; }

    public int Limit { get; set; }
    
    /// <summary>
    /// 端点白名单
    /// </summary>
    public string[] EndpointWhitelist { get; set; }

    /// <summary>
    /// IP白名单
    /// </summary>
    public string[] IpWhitelist { get; set; }
}
