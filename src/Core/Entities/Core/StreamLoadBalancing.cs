namespace Core.Entities.Core;

/// <summary>
/// L4 端口转发的上游负载均衡策略
/// </summary>
public enum StreamLoadBalancing : byte
{
    /// <summary>
    /// 轮询
    /// </summary>
    RoundRobin = 0,

    /// <summary>
    /// 最少连接
    /// </summary>
    LeastConnections = 1,

    /// <summary>
    /// 随机
    /// </summary>
    Random = 2
}
