using System.ComponentModel.DataAnnotations;
using System.Net;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Security;

namespace FastGateway.Services;

public static class AbnormalIpService
{
    private const string AutoBlacklistName = "自动黑名单";

    public static IEndpointRouteBuilder MapAbnormalIp(this IEndpointRouteBuilder app)
    {
        var abnormalIp = app.MapGroup("/api/v1/abnormal-ip")
            .WithTags("安全防护")
            .WithDescription("异常IP监控")
            .RequireAuthorization()
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("异常IP");

        abnormalIp.MapGet(string.Empty, (int page, int pageSize) =>
            {
                page = page < 1 ? 1 : page;
                pageSize = pageSize < 1 ? 10 : pageSize;

                var all = AbnormalIpMonitor.GetAbnormalIps()
                    .Concat(AdaptiveThreatStore.GetActiveSnapshots().Select(ToAbnormalSnapshot))
                    .GroupBy(x => x.Ip, StringComparer.OrdinalIgnoreCase)
                    .Select(x => x.OrderByDescending(y => y.WindowErrorCount).ThenByDescending(y => y.LastSeen).First())
                    .OrderByDescending(x => x.WindowErrorCount)
                    .ThenByDescending(x => x.LastSeen)
                    .ToList();
                var total = all.Count;
                var result = all
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return new PagingDto<AbnormalIpSnapshot>(total, result);
            })
            .WithDescription("获取异常IP列表")
            .WithDisplayName("获取异常IP列表")
            .WithTags("异常IP");

        abnormalIp.MapDelete("adaptive", (string ip) =>
            {
                ip = NormalizeIp(ip);
                if (string.IsNullOrWhiteSpace(ip) || !IPAddress.TryParse(ip, out _))
                    throw new ValidationException("IP格式不正确");

                var released = AdaptiveThreatStore.Release(ip);
                return ResultDto.CreateSuccess(released ? "已解除临时隔离" : "IP不在临时隔离中");
            })
            .WithDescription("解除自适应威胁产生的临时隔离")
            .WithDisplayName("解除临时隔离")
            .WithTags("异常IP");

        abnormalIp.MapPost("blacklist", (ConfigurationService configService, AddAbnormalIpToBlacklistInput input) =>
            {
                var ip = NormalizeIp(input.Ip);
                if (string.IsNullOrWhiteSpace(ip)) throw new ValidationException("IP不能为空");
                if (!IPAddress.TryParse(ip, out _)) throw new ValidationException("IP格式不正确");

                var allItems = configService.GetBlacklistAndWhitelists();
                var autoBlacklist = allItems.FirstOrDefault(x =>
                    x is { IsBlacklist: true } &&
                    string.Equals(x.Name, AutoBlacklistName, StringComparison.OrdinalIgnoreCase));

                if (autoBlacklist == null)
                {
                    autoBlacklist = new BlacklistAndWhitelist
                    {
                        Name = AutoBlacklistName,
                        Description = "由异常IP监控自动加入",
                        Enable = true,
                        IsBlacklist = true,
                        Ips = new List<string> { ip }
                    };

                    configService.AddBlacklistAndWhitelist(autoBlacklist);
                }
                else
                {
                    autoBlacklist.Ips ??= new List<string>();
                    if (!autoBlacklist.Ips.Contains(ip, StringComparer.OrdinalIgnoreCase))
                    {
                        autoBlacklist.Ips.Add(ip);
                        configService.UpdateBlacklistAndWhitelist(autoBlacklist);
                    }
                }

                BlacklistAndWhitelistService.RefreshCache(configService);

                return ResultDto.CreateSuccess("已加入黑名单");
            })
            .WithDescription("将异常IP加入黑名单")
            .WithDisplayName("将异常IP加入黑名单")
            .WithTags("异常IP");

        abnormalIp.MapDelete("blacklist", (ConfigurationService configService, string ip) =>
            {
                ip = NormalizeIp(ip);
                if (string.IsNullOrWhiteSpace(ip)) throw new ValidationException("IP不能为空");
                if (!IPAddress.TryParse(ip, out _)) throw new ValidationException("IP格式不正确");

                var allItems = configService.GetBlacklistAndWhitelists();
                var autoBlacklist = allItems.FirstOrDefault(x =>
                    x is { IsBlacklist: true } &&
                    string.Equals(x.Name, AutoBlacklistName, StringComparison.OrdinalIgnoreCase));

                if (autoBlacklist == null || autoBlacklist.Ips == null || autoBlacklist.Ips.Count == 0)
                {
                    return ResultDto.CreateSuccess("IP不在黑名单");
                }

                var removedCount = autoBlacklist.Ips.RemoveAll(x => string.Equals(x, ip, StringComparison.OrdinalIgnoreCase));
                if (removedCount <= 0)
                {
                    return ResultDto.CreateSuccess("IP不在黑名单");
                }

                if (autoBlacklist.Ips.Count == 0)
                {
                    configService.DeleteBlacklistAndWhitelist(autoBlacklist.Id);
                }
                else
                {
                    configService.UpdateBlacklistAndWhitelist(autoBlacklist);
                }

                BlacklistAndWhitelistService.RefreshCache(configService);

                return ResultDto.CreateSuccess("已移出黑名单");
            })
            .WithDescription("将异常IP移出黑名单")
            .WithDisplayName("将异常IP移出黑名单")
            .WithTags("异常IP");

        return app;
    }

    private static AbnormalIpSnapshot ToAbnormalSnapshot(AdaptiveThreatSnapshot item)
    {
        return new AbnormalIpSnapshot
        {
            Ip = item.Ip,
            WindowErrorCount = item.Score,
            TotalErrorCount = item.TotalRequests,
            FirstSeen = item.FirstSeen.LocalDateTime,
            LastSeen = item.LastSeen.LocalDateTime,
            LastErrorDescription = item.Reason,
            TopErrorDescription = item.Reason,
            LastPath = item.LastPath,
            LastMethod = item.LastMethod,
            LastStatusCode = item.LastStatus,
            LastServerId = item.ServerId
        };
    }

    private static string NormalizeIp(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var candidate = raw.Split(',')[0].Trim();
        if (IPAddress.TryParse(candidate, out var address)) return address.ToString();
        if (IPEndPoint.TryParse(candidate, out var endpoint)) return endpoint.Address.ToString();
        return candidate;
    }
}

public sealed class AddAbnormalIpToBlacklistInput
{
    public string Ip { get; set; } = string.Empty;
}
