namespace FastGateway.Tunnels;

public class AgentState
{
    /// <summary>
    ///     客户端实例
    /// </summary>
    public required AgentClient Client { get; init; }

    /// <summary>
    ///     是否为连接状态
    /// </summary>
    public required bool IsConnected { get; init; }
}