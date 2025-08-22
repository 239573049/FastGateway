using FastGateway.Tunnels;
using Yarp.ReverseProxy.Forwarder;

namespace FastGateway.Extensions;

public static class ServiceExtensions
{
    public static void AddTunnel(this IServiceCollection services)
    {
        services.AddSingleton<AgentTunnelFactory>();
        services.AddSingleton<AgentClientManager>();
        services.AddSingleton<TunnelClientFactory>();
        services.AddSingleton<AgentStateChannel>();
        services.AddSingleton<AgentManagerMiddleware>();
        services.AddSingleton<AgentManagerTunnelMiddleware>();
        services.AddSingleton<TunnelClientProxy>();

        services.AddSingleton<IForwarderHttpClientFactory>(s => s.GetRequiredService<TunnelClientFactory>());
    }
}