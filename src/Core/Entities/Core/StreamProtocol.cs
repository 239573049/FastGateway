namespace Core.Entities.Core;

/// <summary>
/// L4 端口转发协议
/// </summary>
public enum StreamProtocol : byte
{
    /// <summary>
    /// 仅 TCP
    /// </summary>
    Tcp = 0,

    /// <summary>
    /// 仅 UDP
    /// </summary>
    Udp = 1,

    /// <summary>
    /// TCP 与 UDP 同时监听
    /// </summary>
    Both = 2
}
