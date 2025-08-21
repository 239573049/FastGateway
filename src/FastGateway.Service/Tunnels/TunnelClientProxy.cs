using FastGateway.Entities;
using Yarp.ReverseProxy.Configuration;

namespace FastGateway.Service.Tunnels;

public class TunnelClientProxy(InMemoryConfigProvider inMemoryConfigProvider)
{
    private readonly Dictionary<string, Tunnel> _tunnelClients = new();

    public List<Tunnel> GetAllClients()
    {
        return this._tunnelClients.Values.Select(x => x).ToList();
    }

    public void CreateClient(Tunnel tunnel, Server server, DomainName[] domainNames)
    {
        if (_tunnelClients.ContainsKey(tunnel.Name))
        {
            // 更新
            _tunnelClients[tunnel.Name] = tunnel;
        }
        else
        {
            // 添加
            _tunnelClients.Add(tunnel.Name, tunnel);
        }

        // 更新配置
        Gateway.Gateway.ReloadGateway(server, [..domainNames]);
    }

    public async Task RemoveClientAsync(string name)
    {
        _tunnelClients.Remove(name);
        // 更新配置

        await Task.CompletedTask;
    }
}