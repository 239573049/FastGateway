namespace FastGateway.Dto;

public class TunnelNodeDto
{
    /// <summary>
    /// 节点名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 节点描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 令牌
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// 服务器地址
    /// </summary>
    public string ServerUrl { get; set; } = string.Empty;

    /// <summary>
    /// 重连间隔
    /// </summary>
    public int ReconnectInterval { get; set; }

    /// <summary>
    /// 心跳间隔
    /// </summary>
    public int HeartbeatInterval { get; set; }

    /// <summary>
    /// 代理数量
    /// </summary>
    public int ProxyCount { get; set; }

    /// <summary>
    /// 代理配置列表
    /// </summary>
    public TunnelProxyDto[] Proxies { get; set; } = Array.Empty<TunnelProxyDto>();

    /// <summary>
    /// 节点状态
    /// </summary>
    public bool IsOnline { get; set; }

    /// <summary>
    /// 最后连接时间
    /// </summary>
    public DateTime? LastConnectTime { get; set; }
}

public class TunnelProxyDto
{
    /// <summary>
    /// 唯一标识
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// 主机头
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// 路由
    /// </summary>
    public string Route { get; set; } = string.Empty;

    /// <summary>
    /// 本地远程地址
    /// </summary>
    public string LocalRemote { get; set; } = string.Empty;

    /// <summary>
    /// 描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 域名列表
    /// </summary>
    public string[] Domains { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enabled { get; set; }
}