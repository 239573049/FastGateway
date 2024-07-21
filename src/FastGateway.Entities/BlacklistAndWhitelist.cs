namespace FastGateway.Entities;

/// <summary>
/// 黑白名单
/// </summary>
public sealed class BlacklistAndWhitelist
{
    /// <summary>
    /// 主键ID
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// IP地址列表
    /// </summary>
    public List<string> Ips { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enable { get; set; }
    
    /// <summary>
    /// 是否黑名单
    /// </summary>
    public bool IsBlacklist { get; set; }
}