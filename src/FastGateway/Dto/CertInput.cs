namespace FastGateway.Dto;

public class CertInput
{
    public string[] Domains { get; set; }

    /// <summary>
    /// 自动续期
    /// </summary>
    public bool AutoRenew { get; set; }
    
    /// <summary>
    /// 证书文件
    /// </summary>
    public string CertFile { get; set; }

    /// <summary>
    /// 证书密码
    /// </summary>
    public string CertPassword { get; set; }

    /// <summary>
    /// 邮箱
    /// </summary>
    public string Email { get; set; }
}