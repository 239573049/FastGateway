using Core.Entities;
using Core.Entities.Core;

namespace FastGateway.Dto;

/// <summary>
/// L4 端口转发规则展示对象
/// </summary>
public class StreamForwardDto
{
    public string Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool Enable { get; set; }

    public StreamProtocol Protocol { get; set; }

    public ushort ListenPort { get; set; }

    public string ListenAddress { get; set; } = "0.0.0.0";

    public List<StreamUpStream> UpStreams { get; set; } = new();

    public StreamLoadBalancing LoadBalancing { get; set; }

    public int ConnectTimeoutMs { get; set; }

    public int IdleTimeoutSeconds { get; set; }

    public bool EnableBlacklist { get; set; }

    public bool EnableWhitelist { get; set; }

    /// <summary>
    /// 是否在线（监听中）
    /// </summary>
    public bool OnLine { get; set; }

    /// <summary>
    /// 当前活动 TCP 连接数
    /// </summary>
    public int ActiveConnections { get; set; }

    /// <summary>
    /// 当前 UDP 会话数
    /// </summary>
    public int UdpSessions { get; set; }
}
