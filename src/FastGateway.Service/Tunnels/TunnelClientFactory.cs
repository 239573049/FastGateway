using System.Collections.Concurrent;
using System.Net.Sockets;
using Yarp.ReverseProxy.Forwarder;

namespace FastGateway.Service.Tunnels;


/// <summary>
/// The factory that YARP will use the create outbound connections by host name.
/// </summary>
internal class TunnelClientFactory(AgentClientManager agentClientManager, AgentTunnelFactory agentTunnelFactory)
    : ForwarderHttpClientFactory
{
    // TODO: These values should be populated by configuration so there's no need to remove
    // channels.
    private readonly ConcurrentDictionary<string, List<Stream>> _clusterConnections = new();

    public List<Stream> GetConnectionChannel(string host)
    {
        // 如果存在则更新
        return _clusterConnections.GetOrAdd(host.ToLower(),
            _ => new List<Stream>());
    }

    /// <summary>
    /// 随机获取一个Stream
    /// </summary>
    /// <param name="streams"></param>
    private Stream GetStream(List<Stream> streams)
    {
        var index = new Random().Next(0, streams.Count);

        return streams[index];
    }

    /// <summary>
    /// yarp的网关客户端工厂，当yarp需要创建一个新的请求时，会调用此方法。
    /// </summary>
    /// <param name="context"></param>
    /// <param name="handler"></param>
    protected override void ConfigureHandler(ForwarderHttpClientContext context, SocketsHttpHandler handler)
    {
        base.ConfigureHandler(context, handler);

        var previous = handler.ConnectCallback ?? DefaultConnectCallback;

        static async ValueTask<Stream> DefaultConnectCallback(SocketsHttpConnectionContext context,
            CancellationToken cancellationToken)
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true
            };
            try
            {
                await socket.ConnectAsync(context.DnsEndPoint, cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }


        handler.ConnectCallback = async (context, cancellationToken) =>
        {
            if (agentClientManager.TryGetValue(context.DnsEndPoint.Host.ToLower(), out var agentClient))
            {
                var agentTunnel =
                    await agentTunnelFactory.CreateHttpTunnelAsync(agentClient.Connection, cancellationToken);


                return agentTunnel;
            }
            
            return await previous(context, cancellationToken);
        };

        handler.MaxAutomaticRedirections = 3;
        handler.EnableMultipleHttp2Connections = true;
    }
}