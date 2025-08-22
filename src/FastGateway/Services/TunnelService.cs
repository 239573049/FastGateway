using System.ComponentModel.DataAnnotations;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Tunnels;

namespace FastGateway.Services;

public static class TunnelService
{
    public static IEndpointRouteBuilder MapTunnel(this IEndpointRouteBuilder app)
    {
        var tunnel = app.MapGroup("/api/v1/tunnel")
            .WithTags("节点管理")
            .WithDescription("隧道节点管理")
            .RequireAuthorization()
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("节点管理");

        tunnel.MapGet(string.Empty, () =>
            {
                var tunnels = TunnelClientProxy.GetAllClients();
                return tunnels.Select(t => new TunnelNodeDto
                {
                    Name = t.Name,
                    Token = t.Token,
                    ServerUrl = t.ServerUrl,
                    ReconnectInterval = t.ReconnectInterval,
                    IsOnline = true,
                    HeartbeatInterval = t.HeartbeatInterval,
                    ProxyCount = t.Proxy?.Length ?? 0,
                    Proxies = t.Proxy?.Select(p => new TunnelProxyDto
                    {
                        Id = p.Id,
                        Host = p.Host,
                        Route = p.Route,
                        LocalRemote = p.LocalRemote,
                        Description = p.Description,
                        Domains = p.Domains,
                        Enabled = p.Enabled
                    }).ToArray() ?? []
                }).ToList();
            })
            .WithDescription("获取节点列表")
            .WithDisplayName("获取节点列表")
            .WithTags("节点管理");

        tunnel.MapGet("{name}", (string name) =>
            {
                var tunnels = TunnelClientProxy.GetAllClients();
                var tunnel = tunnels.FirstOrDefault(t => t.Name == name);
                
                if (tunnel == null)
                    throw new ValidationException("节点不存在");
                
                return new TunnelNodeDto
                {
                    Name = tunnel.Name,
                    Token = tunnel.Token,
                    ServerUrl = tunnel.ServerUrl,
                    IsOnline = true,
                    ReconnectInterval = tunnel.ReconnectInterval,
                    HeartbeatInterval = tunnel.HeartbeatInterval,
                    ProxyCount = tunnel.Proxy?.Length ?? 0,
                    Proxies = tunnel.Proxy?.Select(p => new TunnelProxyDto
                    {
                        Id = p.Id,
                        Host = p.Host,
                        Route = p.Route,
                        LocalRemote = p.LocalRemote,
                        Description = p.Description,
                        Domains = p.Domains,
                        Enabled = p.Enabled
                    }).ToArray() ?? []
                };
            })
            .WithDescription("获取节点详情")
            .WithDisplayName("获取节点详情")
            .WithTags("节点管理");

        return app;
    }
}