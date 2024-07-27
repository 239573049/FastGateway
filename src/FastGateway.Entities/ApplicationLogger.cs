namespace FastGateway.Entities;

/// <summary>
/// 网关请求日志
/// </summary>
public sealed class ApplicationLogger
{
    public long Id { get; set; }

    /// <summary>
    /// 请求时间
    /// </summary>
    public DateTime RequestTime { get; set; }

    /// <summary>
    /// 请求路径
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// 请求方法
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    /// 请求状态
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// 请求IP
    /// </summary>
    public string? Ip { get; set; }

    /// <summary>
    /// 请求耗时
    /// </summary>
    public long Elapsed { get; set; }

    /// <summary>
    /// 请求域名
    /// </summary>
    public string? Domain { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public string UserAgent { get; set; }
    
    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// 请求平台
    /// </summary>
    public string? Platform { get; set; }

    /// <summary>
    /// 请求国家
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// 请求地区
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// 扩展参数
    /// </summary>
    public Dictionary<string, string> Extend { get; set; } = new();
}