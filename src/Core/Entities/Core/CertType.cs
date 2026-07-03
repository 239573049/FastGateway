namespace Core.Entities.Core;

/// <summary>
/// 证书来源类型
/// </summary>
public enum CertType : byte
{
    /// <summary>
    /// Let's Encrypt 自动申请
    /// </summary>
    LetsEncrypt = 0,

    /// <summary>
    /// 用户上传的自定义证书
    /// </summary>
    Custom = 1,
}
