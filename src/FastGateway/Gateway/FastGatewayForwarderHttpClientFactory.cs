using FastGateway.Tunnels;
using System.Diagnostics;
using System.Net;
using System.Text;
using Yarp.ReverseProxy.Forwarder;

namespace FastGateway.Gateway;

internal sealed class FastGatewayForwarderHttpClientFactory(
    TunnelClientFactory tunnelClientFactory,
    StandardForwarderHttpClientFactory standardForwarderHttpClientFactory)
    : IForwarderHttpClientFactory
{
    private const string ClientModeMetadataKey = "FastGateway.ClientMode";
    private const string TunnelClientMode = "Tunnel";

    public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
    {
        if (context.NewMetadata is not null &&
            context.NewMetadata.TryGetValue(ClientModeMetadataKey, out var clientMode) &&
            string.Equals(clientMode, TunnelClientMode, StringComparison.OrdinalIgnoreCase))
        {
            return tunnelClientFactory.CreateClient(context);
        }

        return standardForwarderHttpClientFactory.CreateClient(context);
    }
}

public sealed class StandardForwarderHttpClientFactory : ForwarderHttpClientFactory
{
    protected override void ConfigureHandler(ForwarderHttpClientContext context, SocketsHttpHandler handler)
    {
        handler.UseProxy = false;
        handler.AllowAutoRedirect = false;
        handler.AutomaticDecompression = DecompressionMethods.None;
        handler.UseCookies = false;
        handler.ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current);
        handler.RequestHeaderEncodingSelector = (_, _) => Encoding.UTF8;
        // 建连（TCP + TLS 握手）超时。1s 对跨境/高延迟/冷启动上游过短，会间歇性握手失败；
        // 取 10s，落在 YARP 常见区间，避免误杀慢上游。
        handler.ConnectTimeout = TimeSpan.FromSeconds(10);
        handler.PooledConnectionLifetime = TimeSpan.FromMinutes(10);
        handler.PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2);
        handler.ResponseDrainTimeout = TimeSpan.FromSeconds(10);
        handler.EnableMultipleHttp2Connections = true;
        handler.EnableMultipleHttp3Connections = false;
        handler.MaxConnectionsPerServer = context.NewConfig?.MaxConnectionsPerServer ?? 1024;

        base.ConfigureHandler(context, handler);
    }
}
