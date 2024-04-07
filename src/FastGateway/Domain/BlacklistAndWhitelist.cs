namespace FastGateway.Domain;

/// <summary>
/// 黑白名单
/// </summary>
[Table(Name = "blacklist_and_whitelist")]
public sealed class BlacklistAndWhitelist
{
    [Column(IsIdentity = true)]
    public long Id { get; set; }
    
    [Column(MapType = typeof(string), StringLength = -1)]
    public List<string> Ips { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    public bool Enable { get; set; }
    
    public ProtectionType Type { get; set; } = ProtectionType.Blacklist;
}