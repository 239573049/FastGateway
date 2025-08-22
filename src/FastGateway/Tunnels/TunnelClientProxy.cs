using Core.Entities;

namespace FastGateway.Tunnels;

public class TunnelClientProxy
{
    private static readonly Dictionary<string, Tunnel> TunnelClients = new();

    public static List<Tunnel> GetAllClients()
    {
        return TunnelClients.Values.Select(x => x).ToList();
    }

    public void CreateClient(Tunnel tunnel, Server server, DomainName[] domainNames)
    {
        foreach (var tunnelProxy in tunnel.Proxy) tunnelProxy.Id = Guid.NewGuid().ToString("N");
        TunnelClients[tunnel.Name] = tunnel;

        // 更新配置
        Gateway.Gateway.ReloadGateway(server, [..domainNames]);
    }

    public Tunnel.TunnelProxy? GetProxy(string proxyId)
    {
        return TunnelClients.Values.Select(tunnel => tunnel.Proxy.FirstOrDefault(x => x.Id == proxyId))
            .OfType<Tunnel.TunnelProxy>().FirstOrDefault();
    }

    public async Task RemoveClientAsync(string name)
    {
        TunnelClients.Remove(name);
        // 更新配置

        await Task.CompletedTask;
    }
}