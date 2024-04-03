namespace FastGateway.Domain;

/// <summary>
/// 服务管理
/// </summary>
public class Service
{
    public string Id { get; set; }

    /// <summary>
    /// 多个服务名称
    /// </summary>
    public string[] ServiceNames { get; set; }

    /// <summary>
    /// 服务端口
    /// </summary>
    public ushort Listen { get; set; }

    /// <summary>
    /// 是否启用HTTP3 | 强制HTTPS才能启用
    /// </summary>
    public bool EnableHttp3 { get; set; }

    /// <summary>
    /// 是否HTTPS
    /// </summary>
    public bool IsHttps { get; set; }

    /// <summary>
    /// 是否启用流量监控
    /// </summary>
    public bool EnableFlowMonitoring { get; set; }

    /// <summary>
    /// 是否启用请求来源分析
    /// </summary>
    public bool EnableRequestSource { get; set; }

    /// <summary>
    /// 是否启动
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// 是否启用隧道
    /// </summary>
    public bool EnableTunnel { get; set; }

    /// <summary>
    /// 服务配置
    /// </summary>
    public virtual List<Location> Locations { get; set; }

    /// <summary>
    /// 证书地址
    /// </summary>
    public string? SslCertificate { get; set; }

    /// <summary>
    /// 证书密码
    /// </summary>
    public string? SslCertificatePassword { get; set; }
}