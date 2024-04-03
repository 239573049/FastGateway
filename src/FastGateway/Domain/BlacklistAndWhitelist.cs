namespace FastGateway.Domain;

/// <summary>
/// 黑名单
/// </summary>
public sealed class BlacklistAndWhitelist
{
    public long Id { get; set; }

    public List<string> Ips { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    public bool Enable { get; set; }
    
    public ProtectionType Type { get; set; } = ProtectionType.Blacklist;
}