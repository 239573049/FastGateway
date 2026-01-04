using System.ComponentModel.DataAnnotations;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;

namespace FastGateway.Services;

public static class BlacklistAndWhitelistService
{
    private sealed record IpPolicySnapshot(string[] Whitelist, string[] Blacklist);

    private static IpPolicySnapshot _snapshot = new(Array.Empty<string>(), Array.Empty<string>());

    public static void RefreshCache(IEnumerable<BlacklistAndWhitelist> blacklistAndWhitelists)
    {
        if (blacklistAndWhitelists == null)
        {
            Volatile.Write(ref _snapshot, new IpPolicySnapshot(Array.Empty<string>(), Array.Empty<string>()));
            return;
        }

        var whitelist = blacklistAndWhitelists
            .Where(x => x is { Enable: true, IsBlacklist: false })
            .SelectMany(x => x.Ips ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var blacklist = blacklistAndWhitelists
            .Where(x => x is { Enable: true, IsBlacklist: true })
            .SelectMany(x => x.Ips ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Volatile.Write(ref _snapshot, new IpPolicySnapshot(whitelist, blacklist));
    }

    public static void RefreshCache(ConfigurationService configService)
    {
        RefreshCache(configService.GetBlacklistAndWhitelists());
    }

    public static WebApplication UseBlacklistMiddleware(this WebApplication app,
        List<BlacklistAndWhitelist> blacklistAndWhitelists,
        bool enableBlacklist,
        bool enableWhitelist)
    {
        RefreshCache(blacklistAndWhitelists);

        app.Use(async (context, next) =>
        {
            var ip = ClientIpHelper.GetClientIp(context);
            if (string.IsNullOrWhiteSpace(ip))
            {
                await next(context);
                return;
            }

            var snapshot = Volatile.Read(ref _snapshot);

            if (enableWhitelist && snapshot.Whitelist.Length > 0)
            {
                if (snapshot.Whitelist.Any(range => IpHelper.UnsafeCheckIpInIpRange(ip, range)))
                {
                    await next(context);
                    return;
                }

                context.Response.StatusCode = 403;
                return;
            }

            if (enableBlacklist && snapshot.Blacklist.Length > 0 &&
                snapshot.Blacklist.Any(range => IpHelper.UnsafeCheckIpInIpRange(ip, range)))
            {
                context.Response.StatusCode = 403;
                return;
            }

            await next(context);
        });

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
            RefreshCache(configService);
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
                (ConfigurationService configService, long id) =>
                {
                    configService.DeleteBlacklistAndWhitelist(id);
                    RefreshCache(configService);
                })
            .WithDescription("删除黑白名单").WithDisplayName("删除黑白名单").WithTags("黑白名单");

        domain.MapPut("{id}", (ConfigurationService configService, long id, BlacklistAndWhitelist blacklist) =>
        {
            if (string.IsNullOrWhiteSpace(blacklist.Name)) throw new ValidationException("名称不能为空");

            blacklist.Id = id;
            configService.UpdateBlacklistAndWhitelist(blacklist);
            RefreshCache(configService);
        }).WithDescription("更新黑白名单").WithDisplayName("更新黑白名单").WithTags("黑白名单");


        return app;
    }
}
