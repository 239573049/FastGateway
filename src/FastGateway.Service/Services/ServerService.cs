using System.ComponentModel.DataAnnotations;
using FastGateway.Entities;
using FastGateway.Service.DataAccess;
using FastGateway.Service.Dto;
using FastGateway.Service.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.Services;

public static class ServerService
{
    public static WebApplication MapServer(this WebApplication app)
    {
        var server = app.MapGroup("/api/v1/server")
            .WithTags("服务")
            .WithDescription("服务管理")
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("服务");

        server.MapPost(string.Empty, async (MasterContext dbContext, Server server) =>
        {
            if (string.IsNullOrWhiteSpace(server.Name))
            {
                throw new ValidationException("id 不能为空");
            }

            dbContext.Servers.Add(server);

            await dbContext.SaveChangesAsync();
        }).WithDescription("创建服务").WithDisplayName("创建服务").WithTags("服务");

        server.MapGet(string.Empty, async (MasterContext dbContext) =>
            {
                var result = await dbContext.Servers.ToListAsync();

                return result.Select(x => new ServerDto
                {
                    Id = x.Id,
                    Name = x.Name,
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
            async (MasterContext dbContext, string id) =>
            {
                await dbContext.Servers.Where(x => x.Id == id).ExecuteDeleteAsync();
            }).WithDescription("删除服务").WithDisplayName("删除服务").WithTags("服务");

        server.MapPut("{id}", async (MasterContext dbContext, string id, Server server) =>
        {
            if (string.IsNullOrWhiteSpace(server.Name))
            {
                throw new ValidationException("id 不能为空");
            }

            server.Id = id;

            dbContext.Servers.Update(server);

            await dbContext.SaveChangesAsync();
        }).WithDescription("更新服务").WithDisplayName("更新服务").WithTags("服务");

        server.MapPut("{id}/enable", async (MasterContext dbContext, string id) =>
        {
            await dbContext.Servers
                .Where(x => x.Id == id)
                .ExecuteUpdateAsync(i => i.SetProperty(a => a.Enable, a => !a.Enable));
        }).WithDescription("启用/禁用服务").WithDisplayName("启用/禁用服务").WithTags("服务");

        return app;
    }
}