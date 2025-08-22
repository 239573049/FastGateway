using System.ComponentModel.DataAnnotations;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;

namespace FastGateway.Services;

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
                if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor)) ip = forwardedFor;

                // 判断是否在白名单中
                if (whitelist.Any(x => x.Ips.Any(i => IpHelper.UnsafeCheckIpInIpRange(ip, i)))) await next(context);

                context.Response.StatusCode = 403;
            });
        }
        else
        {
            var blacklist = blacklistAndWhitelists.Where(x => x.Enable && x.IsBlacklist).ToArray();

            if (blacklist.Length > 1)
                app.Use(async (context, next) =>
                {
                    var ip = string.Empty;
                    if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor)) ip = forwardedFor;

                    // 判断是否在黑名单中
                    if (blacklist.Any(x => x.Ips.Any(i => IpHelper.UnsafeCheckIpInIpRange(ip, i))))
                    {
                        context.Response.StatusCode = 403;
                        return;
                    }

                    await next(context);
                });
        }

        return app;
    }

    public static IEndpointRouteBuilder MapBlacklistAndWhitelist(this IEndpointRouteBuilder app)
    {
        var domain = app.MapGroup("/api/v1/black-and-white")
            .WithTags("黑白名单")
            .WithDescription("黑白名单管理")
            .RequireAuthorization()
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("黑白名单");


        domain.MapPost(string.Empty, (ConfigurationService configService, BlacklistAndWhitelist whitelist) =>
        {
            if (string.IsNullOrWhiteSpace(whitelist.Name)) throw new ValidationException("名称不能为空");

            whitelist.Ips = whitelist.Ips.Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct().ToList();

            configService.AddBlacklistAndWhitelist(whitelist);
        }).WithDescription("创建黑白名单").WithDisplayName("创建黑白名单").WithTags("黑白名单");

        domain.MapGet(string.Empty, (ConfigurationService configService, bool isBlacklist, int page, int pageSize) =>
            {
                var allItems = configService.GetBlacklistAndWhitelists()
                    .Where(x => x.IsBlacklist == isBlacklist);

                var total = allItems.Count();
                var result = allItems
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return new PagingDto<BlacklistAndWhitelist>(total, result);
            })
            .WithDescription("获取黑名单列表")
            .WithDisplayName("获取黑名单列表")
            .WithTags("黑白名单");

        domain.MapDelete("{id}",
                (ConfigurationService configService, long id) => { configService.DeleteBlacklistAndWhitelist(id); })
            .WithDescription("删除黑白名单").WithDisplayName("删除黑白名单").WithTags("黑白名单");

        domain.MapPut("{id}", (ConfigurationService configService, long id, BlacklistAndWhitelist blacklist) =>
        {
            if (string.IsNullOrWhiteSpace(blacklist.Name)) throw new ValidationException("名称不能为空");

            blacklist.Id = id;
            configService.UpdateBlacklistAndWhitelist(blacklist);
        }).WithDescription("更新黑白名单").WithDisplayName("更新黑白名单").WithTags("黑白名单");


        return app;
    }
}