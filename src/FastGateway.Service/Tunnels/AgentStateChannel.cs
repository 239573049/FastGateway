using System.Threading.Channels;

namespace FastGateway.Service.Tunnels;

public class AgentStateChannel
{
    private readonly bool _hasStateStorages;

    private readonly Channel<AgentState> _channel = Channel.CreateUnbounded<AgentState>();

    /// <summary>
    /// 将客户端状态写入Channel
    /// 确保持久层的性能不影响到ClientManager
    /// </summary>
    /// <param name="client"></param>
    /// <param name="connected"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public ValueTask WriteAsync(AgentClient client, bool connected, CancellationToken cancellationToken)
    {
        if (this._hasStateStorages == false)
        {
            return ValueTask.CompletedTask;
        }

        var clientState = new AgentState
        {
            Client = client,
            IsConnected = connected
        };

        return this._channel.Writer.WriteAsync(clientState, cancellationToken);
    }

    /// <summary>
    /// 从Channel读取所有客户端状态
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public IAsyncEnumerable<AgentState> ReadAllAsync(CancellationToken cancellationToken)
    {
        return this._channel.Reader.ReadAllAsync(cancellationToken);
    }
}