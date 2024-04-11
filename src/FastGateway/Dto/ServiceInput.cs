namespace FastGateway.Dto;

public class ServiceInput
{
    public string? Id { get; set; }
    

    /// <summary>
    /// 服务端口
    /// </summary>
    public ushort Listen { get; set; }

    /// <summary>
    /// 是否HTTPS
    /// </summary>
    public bool IsHttps { get; set; }

    /// <summary>
    /// 是否启用流量监控
    /// </summary>
    public bool EnableFlowMonitoring { get; set; }

    /// <summary>
    /// 启用黑名单
    /// </summary>
    public bool EnableBlacklist { get; set; }

    /// <summary>
    /// 启用白名单 （白名单优先级高，设置了白名单则忽略黑名单）
    /// </summary>
    public bool EnableWhitelist { get; set; }
    
    /// <summary>
    /// 是否启动
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// 是否启用隧道
    /// </summary>
    public bool EnableTunnel { get; set; }

    public string RateLimitName { get; set; }
    
    /// <summary>
    /// 服务配置
    /// </summary>
    public virtual List<LocationInput> Locations { get; set; }
}
