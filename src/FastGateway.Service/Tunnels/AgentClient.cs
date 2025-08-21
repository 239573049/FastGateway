using System.Net;
using Microsoft.AspNetCore.Http.Features;

namespace FastGateway.Service.Tunnels;

public class AgentClient
{
    private volatile bool _disposed;
    public readonly AgentClientConnection Connection;
    private readonly AgentTunnelFactory _httpTunnelFactory;
    private readonly HttpContext _httpContext;
    private readonly Lazy<HttpMessageInvoker> _httpClientLazy;

    public string Id => this.Connection.ClientId;

    public TransportProtocol Protocol => this._httpContext.Features.GetRequiredFeature<IFastFeature>().Protocol;

    public int HttpTunnelCount => this.Connection.HttpTunnelCount;

    public IPEndPoint? RemoteEndpoint
    {
        get
        {
            var connection = this._httpContext.Connection;
            return connection.RemoteIpAddress == null
                ? null
                : new IPEndPoint(connection.RemoteIpAddress, connection.RemotePort);
        }
    }

    public DateTimeOffset CreationTime { get; } = DateTimeOffset.Now;


    public AgentClient(
        AgentClientConnection connection,
        AgentTunnelFactory httpTunnelFactory,
        HttpContext httpContext)
    {
        this.Connection = connection;
        this._httpTunnelFactory = httpTunnelFactory;
        this._httpContext = httpContext;
    }

    public async ValueTask DisposeAsync()
    {
        if (this._disposed == false)
        {
            this._disposed = true;

            if (this._httpClientLazy.IsValueCreated)
            {
                this._httpClientLazy.Value.Dispose();
            }

            await this.Connection.DisposeAsync();
        }
    }

    public override string ToString()
    {
        return this.Id;
    }
}