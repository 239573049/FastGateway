namespace FastGateway.Domain;

/// <summary>
/// 证书管理
/// </summary>
[Table(Name = "cert")]
public sealed class Cert
{
    public string Id { get; set; }

    [Column(MapType = typeof(string), StringLength = -1)]
    public string[] Domains { get; set; }

    /// <summary>
    /// 是否过期
    /// </summary>
    public bool Expired { get; set; }

    /// <summary>
    /// 自动续期
    /// </summary>
    public bool AutoRenew { get; set; }

    /// <summary>
    /// 颁发机构
    /// </summary>
    public string Issuer { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// 最近续期时间
    /// </summary>
    public DateTime? RenewTime { get; set; }

    /// <summary>
    /// 续期状态
    /// </summary>
    public RenewStats RenewStats { get; set; }

    /// <summary>
    /// 有效期
    /// </summary>
    public DateTime? NotAfter { get; set; }

    [Column(MapType = typeof(string), StringLength = -1)]
    public List<CertData> Certs { get; set; } = new();

    public void AddCert(CertData certData)
    {
        Certs.Add(certData);
    }

    public CertData GetCert(string domain)
    {
        return Certs.FirstOrDefault(x => x.Domain == domain);
    }

    public void RemoveCert(string domain)
    {
        Certs.RemoveAll(x => x.Domain == domain);
    }

    public void ClearCerts()
    {
        Certs.Clear();
    }
}

public class CertData
{
    public string File { get; set; }

    public string Password { get; set; }

    public string Domain { get; set; }
}