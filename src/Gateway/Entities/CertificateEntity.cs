namespace Gateway.Entities;

public sealed class CertificateEntity : Entity
{
    /// <summary>
    /// 证书名称
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 域名
    /// </summary>
    public string Host { get; set; }

    /// <summary>
    /// 证书密码
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// 证书路径
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 更新时间
    /// </summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>
    /// 过期时间
    /// </summary>
    public DateTime? ExpirationTime { get; set; }

    public CertificateType Type { get; set; }
}