using System.Net;
using Microsoft.AspNetCore.Http.Features;

namespace FastGateway.Tunnels;

public class AgentClient
{
    private readonly Lazy<HttpMessageInvoker> _httpClientLazy;
    private readonly HttpContext _httpContext;
    private readonly AgentTunnelFactory _httpTunnelFactory;
    public readonly AgentClientConnection Connection;
    private volatile bool _disposed;


    public AgentClient(
        AgentClientConnection connection,
        AgentTunnelFactory httpTunnelFactory,
        HttpContext httpContext)
    {
        Connection = connection;
        _httpTunnelFactory = httpTunnelFactory;
        _httpContext = httpContext;
    }

    public string Id => Connection.ClientId;

    public TransportProtocol Protocol => _httpContext.Features.GetRequiredFeature<IFastFeature>().Protocol;

    public int HttpTunnelCount => Connection.HttpTunnelCount;

    public IPEndPoint? RemoteEndpoint
    {
        get
        {
            var connection = _httpContext.Connection;
            return connection.RemoteIpAddress == null
                ? null
                : new IPEndPoint(connection.RemoteIpAddress, connection.RemotePort);
        }
    }

    public DateTimeOffset CreationTime { get; } = DateTimeOffset.Now;

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;

            if (_httpClientLazy.IsValueCreated) _httpClientLazy.Value.Dispose();

            await Connection.DisposeAsync();
        }
    }

    public override string ToString()
    {
        return Id;
    }
}