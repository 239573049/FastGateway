using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Sockets;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Risk;
using FastGateway.Risk.Model;
using FastGateway.Services.Statistics;

namespace FastGateway.Services;

public static class BlacklistAndWhitelistService
{
    /// <param name="BlacklistNames">精确 IP 规则所在的分组名</param>
    /// <param name="RangeGroups">含 CIDR/区间规则的分组，用于反查命中网段规则的分组名</param>
    /// <param name="WhitelistV6Subnets">白名单中 IPv6 地址（及长于 /64 的网段）所在的 /64</param>
    /// <param name="WhitelistWideV6">白名单中不长于 /64 的 IPv6 网段</param>
    /// <param name="BlacklistExceptJev">除 Jev 自动封禁分组外的黑名单，用于 Jev 排除域名放行；为 null 表示没有 Jev 封禁，等同 Blacklist</param>
    private sealed record IpPolicySnapshot(
        IpPolicyMatcher Whitelist,
        IpPolicyMatcher Blacklist,
        IpPolicyMatcher? BlacklistExceptJev,
        IpPolicyMatcher RegionBlacklist,
        IReadOnlyDictionary<string, IReadOnlyList<string>> BlacklistNames,
        IReadOnlyList<(IpPolicyMatcher Matcher, string Name)> RangeGroups,
        IReadOnlySet<string> WhitelistV6Subnets,
        IpPolicyMatcher WhitelistWideV6);

    private static IpPolicySnapshot _snapshot = EmptySnapshot();

    private static IpPolicySnapshot EmptySnapshot()
    {
        return new IpPolicySnapshot(IpPolicyMatcher.Empty, IpPolicyMatcher.Empty, null,
            IpPolicyMatcher.Empty,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase), [],
            new HashSet<string>(StringComparer.OrdinalIgnoreCase), IpPolicyMatcher.Empty);
    }

    public static void RefreshCache(IEnumerable<BlacklistAndWhitelist> blacklistAndWhitelists)
    {
        if (blacklistAndWhitelists == null)
        {
            Volatile.Write(ref _snapshot, EmptySnapshot());
            return;
        }

        var items = blacklistAndWhitelists as IList<BlacklistAndWhitelist> ?? blacklistAndWhitelists.ToList();

        var whitelist = items
            .Where(x => x is { Enable: true, IsBlacklist: false })
            .SelectMany(x => x.Ips ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();

        // 黑名单强制启用，不受 Enable 开关影响（安全防护不可关闭）
        var blacklistGroups = items.Where(x => x.IsBlacklist).ToList();
        var blacklist = blacklistGroups
            .SelectMany(x => x.Ips ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();
        var blacklistExceptJev = blacklistGroups
            .Where(x => !string.Equals(x.Name, JevBanService.BlacklistGroupName, StringComparison.Ordinal))
            .SelectMany(x => x.Ips ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

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

        var rangeGroups = blacklistGroups
            .Select(group => (Rules: (group.Ips ?? []).Where(IsRangeRule).ToList(), group.Name))
            .Where(x => x.Rules.Count > 0)
            .Select(x => (IpPolicyMatcher.Build(x.Rules), x.Name))
            .ToList();

        var (whitelistV6Subnets, whitelistWideV6) = BuildWhitelistV6Index(whitelist);

        Volatile.Write(ref _snapshot,
            new IpPolicySnapshot(IpPolicyMatcher.Build(whitelist), IpPolicyMatcher.Build(blacklist),
                blacklistExceptJev.Count == blacklist.Count ? null : IpPolicyMatcher.Build(blacklistExceptJev),
                regionMatcher,
                blacklistNames, rangeGroups, whitelistV6Subnets, whitelistWideV6));
    }

    private static bool IsRangeRule(string? rule)
    {
        return !string.IsNullOrWhiteSpace(rule) && (rule.Contains('/') || rule.Contains('-'));
    }

    private static (HashSet<string> Subnets, IpPolicyMatcher Wide) BuildWhitelistV6Index(List<string> rules)
    {
        var subnets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var wide = new List<string>();
        foreach (var rule in rules)
        {
            var slash = rule.IndexOf('/');
            var baseText = slash > 0 ? rule[..slash] : rule;
            if (!IPAddress.TryParse(baseText, out var address) ||
                address.AddressFamily != AddressFamily.InterNetworkV6 || address.IsIPv4MappedToIPv6)
                continue;

            if (slash > 0 && int.TryParse(rule[(slash + 1)..], out var prefix) && prefix <= 64)
                wide.Add(rule);
            else if (IpSubnet.GetKey(baseText) is { } key)
                subnets.Add(key);
        }

        return (subnets, wide.Count == 0 ? IpPolicyMatcher.Empty : IpPolicyMatcher.Build(wide));
    }

    /// <summary>
    ///     IPv6 /64 主体（如 "2001:db8::/64"）是否与白名单有交集：封禁整段前必须确认不会误伤白名单地址。
    /// </summary>
    public static bool WhitelistOverlaps(string subnet)
    {
        var slash = subnet.IndexOf('/');
        if (slash <= 0) return IsWhitelisted(subnet);
        var snapshot = Volatile.Read(ref _snapshot);
        return snapshot.WhitelistV6Subnets.Contains(subnet) || snapshot.WhitelistWideV6.Contains(subnet[..slash]);
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
        var result = new List<string>();
        if (snapshot.BlacklistNames.TryGetValue(ip, out var names)) result.AddRange(names);
        foreach (var (matcher, name) in snapshot.RangeGroups)
            if (matcher.Contains(ip) && !result.Contains(name, StringComparer.OrdinalIgnoreCase))
                result.Add(name);
        if (result.Count == 0 && snapshot.RegionBlacklist.Contains(ip)) result.Add("地区封禁");
        return result;
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
                if (snapshot.Blacklist.Contains(ip) && !IsJevOnlyBanOnExcludedHost(snapshot, ip, context))
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

    /// <summary>仅因 Jev 自动封禁命中、且访问的是 Jev 排除域名时放行。</summary>
    private static bool IsJevOnlyBanOnExcludedHost(IpPolicySnapshot snapshot, string ip, HttpContext context)
    {
        if (!RiskModelProviderStore.Current.IsHostExcluded(context.Request.Host.Host)) return false;
        var other = snapshot.BlacklistExceptJev ?? snapshot.Blacklist;
        return !other.Contains(ip);
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
