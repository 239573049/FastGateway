namespace FastGateway.Entities;

/// <summary>
/// 端口管理
/// </summary>
[Index("port_management_name", nameof(Name), true)]
public sealed class PortManagementEntity : Entity
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public string Id { get; set; } = null!;

    public string Name { get; set; } = null!;

    /// <summary>
    /// 描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 监听端口号
    /// </summary>
    public ushort Port { get; set; }

    /// <summary>
    /// 是否https
    /// </summary>
    public bool IsHttps { get; set; }

    /// <summary>
    /// 是否启用流量监控
    /// </summary>
    public bool EnableFlowMonitoring { get; set; }

    /// <summary>
    /// 是否启用http3
    /// </summary>
    public bool EnableHttp3 { get; set; }

    /// <summary>
    /// 请求Body最大长度
    /// </summary>
    public long MaxRequestBodySize { get; set; }

    /// <summary>
    /// 是否启用请求来源分析
    /// </summary>
    public bool EnableRequestSource { get; set; }

    /// <summary>
    /// 启用离线归属地分析
    /// 前提是启用请求来源分析
    /// </summary>
    public bool EnableOfflineHomeAddress { get; set; }

    /// <summary>
    /// 是否启动
    /// </summary>
    public bool Start { get; set; }

    /// <summary>
    /// 是否启用隧道
    /// </summary>
    public bool EnableTunnel { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enable { get; set; }
}