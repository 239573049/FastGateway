namespace FastGateway.Domain;

/// <summary>
/// 黑白名单
/// </summary>
[Table(Name = "blacklist_and_whitelist")]
public sealed class BlacklistAndWhitelist
{
    /// <summary>
    /// 主键ID
    /// </summary>
    [Column(IsIdentity = true)]
    public long Id { get; set; }
    
    /// <summary>
    /// IP地址列表
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
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
    /// 保护类型，默认为黑名单
    /// </summary>
    public ProtectionType Type { get; set; } = ProtectionType.Blacklist;
}