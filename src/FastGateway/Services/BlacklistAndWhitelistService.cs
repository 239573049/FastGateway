using System.ComponentModel.DataAnnotations;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Services.Statistics;

namespace FastGateway.Services;

public static class BlacklistAndWhitelistService
{
    private sealed record IpPolicySnapshot(
        IpPolicyMatcher Whitelist,
        IpPolicyMatcher Blacklist,
        IpPolicyMatcher RegionBlacklist,
        IReadOnlyDictionary<string, IReadOnlyList<string>> BlacklistNames);

    private static IpPolicySnapshot _snapshot =
        new(IpPolicyMatcher.Empty, IpPolicyMatcher.Empty, IpPolicyMatcher.Empty,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));

    public static void RefreshCache(IEnumerable<BlacklistAndWhitelist> blacklistAndWhitelists)
    {
        if (blacklistAndWhitelists == null)
        {
            Volatile.Write(ref _snapshot,
                new IpPolicySnapshot(IpPolicyMatcher.Empty, IpPolicyMatcher.Empty, IpPolicyMatcher.Empty,
                    new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)));
            return;
        }

        var items = blacklistAndWhitelists as IList<BlacklistAndWhitelist> ?? blacklistAndWhitelists.ToList();

        var whitelist = items
            .Where(x => x is { Enable: true, IsBlacklist: false })
            .SelectMany(x => x.Ips ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x));

        // 黑名单强制启用，不受 Enable 开关影响（安全防护不可关闭）
        var blacklistGroups = items.Where(x => x.IsBlacklist).ToList();
        var blacklist = blacklistGroups
            .SelectMany(x => x.Ips ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x));

        // 地区规则在此一次性预编译为 IPv4 区间（扫描 ip2region.xdb 段索引），请求路径零 GeoIP 查询
        var regionRules = items
            .Where(x => x.IsBlacklist)
            .SelectMany(x => x.Regions ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var regionMatcher = regionRules.Count == 0
            ? IpPolicyMatcher.Empty
            : IpPolicyMatcher.BuildFromRanges(RegionIpRangeProvider.GetRanges(regionRules));

        var blacklistNames = blacklistGroups
            .SelectMany(group => (group.Ips ?? [])
                .Where(ip => !string.IsNullOrWhiteSpace(ip))
                .Select(ip => (Ip: ip.Trim(), Name: group.Name)))
            .GroupBy(x => x.Ip, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key,
                x => (IReadOnlyList<string>)x.Select(y => y.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                StringComparer.OrdinalIgnoreCase);

        Volatile.Write(ref _snapshot,
            new IpPolicySnapshot(IpPolicyMatcher.Build(whitelist), IpPolicyMatcher.Build(blacklist), regionMatcher,
                blacklistNames));
    }

    public static void RefreshCache(ConfigurationService configService)
    {
        RefreshCache(configService.GetBlacklistAndWhitelists());
    }

    /// <summary>
    ///     基于当前黑白名单缓存判定来源 IP 是否放行（供 L4 端口转发等非 HTTP 场景复用）。
    ///     语义与 <see cref="UseBlacklistMiddleware" /> 一致：白名单优先，命中白名单放行、否则拒绝；
    ///     其次黑名单，命中黑名单拒绝；无匹配规则时放行。
    /// </summary>
    /// <param name="ip">来源 IP（字符串形式）。为空时放行（无法判定）。</param>
    /// <param name="enableBlacklist">是否启用黑名单</param>
    /// <param name="enableWhitelist">是否启用白名单</param>
    /// <returns>是否允许通过</returns>
    public static bool IsAllowed(string? ip, bool enableBlacklist, bool enableWhitelist)
    {
        if (string.IsNullOrWhiteSpace(ip)) return true;

        var snapshot = Volatile.Read(ref _snapshot);

        if (enableWhitelist && !snapshot.Whitelist.IsEmpty)
            return snapshot.Whitelist.Contains(ip);

        if (enableBlacklist && (snapshot.Blacklist.Contains(ip) || snapshot.RegionBlacklist.Contains(ip)))
            return false;

        return true;
    }

    public static bool IsWhitelisted(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return false;
        return Volatile.Read(ref _snapshot).Whitelist.Contains(ip);
    }

    public static bool IsBlacklisted(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return false;
        var snapshot = Volatile.Read(ref _snapshot);
        return snapshot.Blacklist.Contains(ip) || snapshot.RegionBlacklist.Contains(ip);
    }

    public static IReadOnlyList<string> GetBlacklistNames(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return [];
        var snapshot = Volatile.Read(ref _snapshot);
        if (snapshot.BlacklistNames.TryGetValue(ip, out var names) && names.Count > 0) return names;
        return snapshot.RegionBlacklist.Contains(ip) ? ["地区封禁"] : [];
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

            if (enableWhitelist && !snapshot.Whitelist.IsEmpty)
            {
                if (snapshot.Whitelist.Contains(ip))
                {
                    await next(context);
                    return;
                }

                context.Items[StatisticsCollector.BlockReasonKey] = (byte)BlockReason.Whitelist;
                context.Response.StatusCode = 403;
                return;
            }

            if (enableBlacklist)
            {
                if (snapshot.Blacklist.Contains(ip))
                {
                    context.Items[StatisticsCollector.BlockReasonKey] = (byte)BlockReason.Blacklist;
                    context.Response.StatusCode = 403;
                    return;
                }

                if (snapshot.RegionBlacklist.Contains(ip))
                {
                    context.Items[StatisticsCollector.BlockReasonKey] = (byte)BlockReason.Region;
                    context.Response.StatusCode = 403;
                    return;
                }
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

            NormalizeRules(whitelist);

            configService.AddBlacklistAndWhitelist(whitelist);
            RefreshCache(configService);
        }).WithDescription("创建黑白名单").WithDisplayName("创建黑白名单").WithTags("黑白名单");

        domain.MapGet("regions", (ILoggerFactory loggerFactory) =>
                RegionIpRangeProvider.GetAvailableRegions(loggerFactory.CreateLogger(nameof(RegionIpRangeProvider))))
            .WithDescription("获取可选地区列表（国家与中国省份，来自 ip2region.xdb）")
            .WithDisplayName("获取可选地区列表")
            .WithTags("黑白名单");

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

            NormalizeRules(blacklist);

            blacklist.Id = id;
            configService.UpdateBlacklistAndWhitelist(blacklist);
            RefreshCache(configService);
        }).WithDescription("更新黑白名单").WithDisplayName("更新黑白名单").WithTags("黑白名单");


        return app;
    }

    /// <summary>
    ///     去重去空；地区规则仅黑名单支持，黑名单允许只配置地区不配置 IP。
    /// </summary>
    private static void NormalizeRules(BlacklistAndWhitelist item)
    {
        item.Ips = (item.Ips ?? []).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim()).Distinct().ToList();

        item.Regions = item.IsBlacklist
            ? (item.Regions ?? []).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim()).Distinct().ToList()
            : null;

        if (item.Ips.Count == 0 && (item.Regions?.Count ?? 0) == 0)
            throw new ValidationException("IP 列表与地区列表不能同时为空");
    }
}
