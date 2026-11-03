using System.ComponentModel.DataAnnotations;
using System.Net;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;

namespace FastGateway.Services;

public static class AbnormalIpService
{
    private const string AutoBlacklistName = "自动黑名单";

    public static IEndpointRouteBuilder MapAbnormalIp(this IEndpointRouteBuilder app)
    {
        var abnormalIp = app.MapGroup("/api/v1/abnormal-ip")
            .WithTags("安全防护")
            .WithDescription("手动封禁/解封 IP")
            .RequireAuthorization()
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("异常IP");

        abnormalIp.MapPost("blacklist", (ConfigurationService configService, AddAbnormalIpToBlacklistInput input) =>
            {
                var ip = NormalizeIp(input.Ip);
                if (string.IsNullOrWhiteSpace(ip)) throw new ValidationException("IP不能为空");
                if (!IPAddress.TryParse(ip, out _)) throw new ValidationException("IP格式不正确");

                configService.AddIpToBlacklistGroup(AutoBlacklistName, "手动封禁加入", ip);
                BlacklistAndWhitelistService.RefreshCache(configService);

                return ResultDto.CreateSuccess("已加入黑名单");
            })
            .WithDescription("将异常IP加入黑名单")
            .WithDisplayName("将异常IP加入黑名单")
            .WithTags("异常IP");

        abnormalIp.MapDelete("blacklist", (ConfigurationService configService, JevBanService jevBanService, string ip) =>
            {
                ip = NormalizeIp(ip);
                if (string.IsNullOrWhiteSpace(ip)) throw new ValidationException("IP不能为空");
                if (!IPAddress.TryParse(ip, out _)) throw new ValidationException("IP格式不正确");

                // 只解封系统自动维护的分组（手动封禁 + Jev 封禁），用户自建分组需在黑白名单页面管理；
                // Jev 解封会同时记录豁免期，避免同一误判被下一轮重新封禁
                var removedCount = configService.RemoveIpFromBlacklistGroups([AutoBlacklistName], ip) +
                                   jevBanService.Unban(ip);
                if (removedCount <= 0)
                {
                    return ResultDto.CreateSuccess("IP不在黑名单");
                }

                BlacklistAndWhitelistService.RefreshCache(configService);

                return ResultDto.CreateSuccess("已移出黑名单");
            })
            .WithDescription("将异常IP移出黑名单")
            .WithDisplayName("将异常IP移出黑名单")
            .WithTags("异常IP");

        return app;
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
