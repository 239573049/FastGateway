using System.ComponentModel.DataAnnotations;
using FastGateway.Entities;
using FastGateway.Service.DataAccess;
using FastGateway.Service.Dto;
using FastGateway.Service.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.Services;

public static class BlacklistAndWhitelistService
{
    public static WebApplication UseBlacklistMiddleware(this WebApplication app,
        List<BlacklistAndWhitelist> blacklistAndWhitelists)
    {
        var whitelist = blacklistAndWhitelists.Where(x => x.Enable && !x.IsBlacklist).ToArray();

        if (whitelist.Length > 1)
        {
            app.Use(async (context, next) =>
            {
                var ip = string.Empty;
                if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
                {
                    ip = forwardedFor;
                }

                // 判断是否在白名单中
                if (whitelist.Any(x => x.Ips.Any(i => IpHelper.UnsafeCheckIpInIpRange(ip, i))))
                {
                    await next(context);
                }

                context.Response.StatusCode = 403;
            });
        }
        else
        {
            var blacklist = blacklistAndWhitelists.Where(x => x.Enable && x.IsBlacklist).ToArray();

            if (blacklist.Length > 1)
            {
                app.Use(async (context, next) =>
                {
                    var ip = string.Empty;
                    if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
                    {
                        ip = forwardedFor;
                    }

                    // 判断是否在黑名单中
                    if (blacklist.Any(x => x.Ips.Any(i => IpHelper.UnsafeCheckIpInIpRange(ip, i))))
                    {
                        context.Response.StatusCode = 403;
                        return;
                    }

                    await next(context);
                });
            }
        }

        return app;
    }

    public static WebApplication UseBlacklistAndWhitelist(this WebApplication app)
    {
        var domain = app.MapGroup("/api/v1/black-and-white")
            .WithTags("黑白名单")
            .WithDescription("黑白名单管理")
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("黑白名单");


        domain.MapPost(string.Empty, async (MasterContext dbContext, BlacklistAndWhitelist whitelist) =>
        {
            if (string.IsNullOrWhiteSpace(whitelist.Name))
            {
                throw new ValidationException("名称不能为空");
            }

            whitelist.Ips = whitelist.Ips.Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct().ToList();

            dbContext.BlacklistAndWhitelists.Add(whitelist);

            await dbContext.SaveChangesAsync();
        }).WithDescription("创建黑白名单").WithDisplayName("创建黑白名单").WithTags("黑白名单");

        domain.MapGet(string.Empty, async (MasterContext dbContext, bool isBlacklist, int page, int pageSize) =>
            {
                var result = await dbContext.BlacklistAndWhitelists
                    .Where(x => x.IsBlacklist == isBlacklist)
                    .Skip((page - 1) * pageSize)
                    .ToListAsync();

                var total = await dbContext.BlacklistAndWhitelists.CountAsync();

                return new PagingDto<BlacklistAndWhitelist>(total, result);
            })
            .WithDescription("获取黑名单列表")
            .WithDisplayName("获取黑名单列表")
            .WithTags("黑白名单");

        domain.MapDelete("{id}",
            async (MasterContext dbContext, long id) =>
            {
                await dbContext.BlacklistAndWhitelists.Where(x => x.Id == id).ExecuteDeleteAsync();
            }).WithDescription("删除黑白名单").WithDisplayName("删除黑白名单").WithTags("黑白名单");

        domain.MapPut("{id}", async (MasterContext dbContext, long id, BlacklistAndWhitelist blacklist) =>
        {
            if (string.IsNullOrWhiteSpace(blacklist.Name))
            {
                throw new ValidationException("名称不能为空");
            }

            blacklist.Id = id;

            dbContext.BlacklistAndWhitelists.Update(blacklist);

            await dbContext.SaveChangesAsync();
        }).WithDescription("更新黑白名单").WithDisplayName("更新黑白名单").WithTags("黑白名单");


        return app;
    }
}