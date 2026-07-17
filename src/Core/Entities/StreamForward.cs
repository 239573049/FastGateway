using Core.Entities.Core;

namespace Core.Entities;

/// <summary>
/// L4 端口转发规则（TCP/UDP 裸转发，类似 nginx stream / iptables DNAT）
/// </summary>
public sealed class StreamForward
{
    public string Id { get; set; }

    /// <summary>
    /// 规则名称
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 规则描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 是否启用（随网关启动自动监听）
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// 转发协议（TCP / UDP / 两者）
    /// </summary>
    public StreamProtocol Protocol { get; set; }

    /// <summary>
    /// 监听端口
    /// </summary>
    public ushort ListenPort { get; set; }

    /// <summary>
    /// 监听地址，默认 0.0.0.0（全部网卡）
    /// </summary>
    public string ListenAddress { get; set; } = "0.0.0.0";

    /// <summary>
    /// 上游目标列表（支持多个 + 负载均衡）
    /// </summary>
    public List<StreamUpStream> UpStreams { get; set; } = new();

    /// <summary>
    /// 负载均衡策略
    /// </summary>
    public StreamLoadBalancing LoadBalancing { get; set; } = StreamLoadBalancing.RoundRobin;

    /// <summary>
    /// 上游连接超时（毫秒，TCP 生效）
    /// </summary>
    public int ConnectTimeoutMs { get; set; } = 5000;

    /// <summary>
    /// 空闲超时（秒）：TCP 连接空闲断开 / UDP 会话过期回收
    /// </summary>
    public int IdleTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// 启用黑名单（来源 IP 访问控制）
    /// </summary>
    public bool EnableBlacklist { get; set; } = true;

    /// <summary>
    /// 启用白名单（白名单优先级高，设置了白名单则忽略黑名单）
    /// </summary>
    public bool EnableWhitelist { get; set; }
}
