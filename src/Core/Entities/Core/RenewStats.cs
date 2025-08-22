namespace Core.Entities.Core;

/// <summary>
/// 限流类型
/// </summary>
public enum RenewStats  : byte
{
    /// <summary>
    /// 无
    /// </summary>
    None = 0,

    /// <summary>
    /// 成功
    /// </summary>
    Success = 1,

    /// <summary>
    /// 失败
    /// </summary>
    Fail = 2,
}