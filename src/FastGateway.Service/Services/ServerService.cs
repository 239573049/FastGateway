using System.ComponentModel.DataAnnotations;
using FastGateway.Entities;
using FastGateway.Service.Dto;
using FastGateway.Service.Infrastructure;

namespace FastGateway.Service.Services;

public static class ServerService
{
    public static IEndpointRouteBuilder MapServer(this IEndpointRouteBuilder app)
    {
        var server = app.MapGroup("/api/v1/server")
            .WithTags("服务")
            .WithDescription("服务管理")
            .AddEndpointFilter<ResultFilter>()
            .RequireAuthorization()
            .WithDisplayName("服务");

        server.MapPost(string.Empty, async (ConfigurationService configService, Server server) =>
        {
            if (string.IsNullOrWhiteSpace(server.Name))
            {
                throw new ValidationException("id 不能为空");
            }

            configService.AddServer(server);
        }).WithDescription("创建服务").WithDisplayName("创建服务").WithTags("服务");

        server.MapGet(string.Empty, (ConfigurationService configService) =>
            {
                var result = configService.GetServers();

                return result.Select(x => new ServerDto
                {
                    Id = x.Id,
                    Name = x.Name,
                    CopyRequestHost = x.CopyRequestHost,
                    Enable = x.Enable,
                    IsHttps = x.IsHttps,
                    Listen = x.Listen,
                    OnLine = Gateway.Gateway.CheckServerOnline(x.Id),
                    RedirectHttps = x.RedirectHttps,
                    StaticCompress = x.StaticCompress,
                    EnableBlacklist = x.EnableBlacklist,
                    EnableTunnel = x.EnableTunnel,
                    EnableWhitelist = x.EnableWhitelist,
                    Description = x.Description,
                });
            })
            .WithDescription("获取服务列表")
            .WithDisplayName("获取服务列表")
            .WithTags("服务");

        server.MapDelete("{id}",
            async (ConfigurationService configService, string id) =>
            {
                configService.DeleteServer(id);

                await Gateway.Gateway.CloseGateway(id);
            }).WithDescription("删除服务").WithDisplayName("删除服务").WithTags("服务");

        server.MapPut("{id}", (ConfigurationService configService, string id, Server server) =>
        {
            if (string.IsNullOrWhiteSpace(server.Name))
            {
                throw new ValidationException("id 不能为空");
            }

            server.Id = id;
            configService.UpdateServer(server);
        }).WithDescription("更新服务").WithDisplayName("更新服务").WithTags("服务");

        server.MapPut("{id}/enable", (ConfigurationService configService, string id) =>
        {
            var server = configService.GetServer(id);
            if (server != null)
            {
                server.Enable = !server.Enable;
                configService.UpdateServer(server);
            }
        }).WithDescription("启用/禁用服务").WithDisplayName("启用/禁用服务").WithTags("服务");

        // 启用服务
        server.MapPut("{id}/online", async (ConfigurationService configService, string id) =>
        {
            if (!Gateway.Gateway.CheckServerOnline(id))
            {
                var server = configService.GetServer(id);
                var domainNames = configService.GetDomainNamesByServerId(id);
                var blacklistAndWhitelists = configService.GetBlacklistAndWhitelists();
                var rateLimits = configService.GetRateLimits();
                await Task.Factory.StartNew(async () =>
                    await Gateway.Gateway.BuilderGateway(server, domainNames, blacklistAndWhitelists, rateLimits));

                for (int i = 0; i < 10; i++)
                {
                    if (Gateway.Gateway.CheckServerOnline(id))
                    {
                        break;
                    }

                    await Task.Delay(1000);
                }
            }
            else
            {
                await Gateway.Gateway.CloseGateway(id);
            }
        }).WithDescription("启用服务").WithDisplayName("启用服务").WithTags("服务");

        // 重载路由
        server.MapPut("{id}/reload", async (ConfigurationService configService, string id) =>
        {
            var server = configService.GetServer(id);
            if (server == null)
            {
                throw new ValidationException("服务不存在");
            }

            var domainNames = configService.GetDomainNamesByServerId(id);

            Gateway.Gateway.ReloadGateway(server, [..domainNames]);

            await Task.Delay(1000);
        }).WithDescription("重载服务").WithDisplayName("重载服务").WithTags("服务");

        return app;
    }
}