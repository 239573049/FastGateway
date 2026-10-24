using System.ComponentModel.DataAnnotations;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Risk;
using FastGateway.Services.Statistics;

namespace FastGateway.Services;

public static class SettingService
{
    public static IEndpointRouteBuilder MapSetting(this IEndpointRouteBuilder app)
    {
        var setting = app.MapGroup("/api/v1/setting")
            .WithTags("设置")
            .RequireAuthorization()
            .WithDescription("设置管理")
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("设置");

        setting.MapGet(string.Empty,
                async (SettingProvide settingProvide) => (await settingProvide.GetListAsync())
                    .Where(x => !RiskSigningKey.IsProtectedSetting(x.Key))
                    .ToList())
            .WithDescription("获取设置列表").WithDisplayName("获取设置列表").WithTags("设置");

        setting.MapGet("{key}",
                async (SettingProvide settingProvide, string key) =>
                {
                    if (RiskSigningKey.IsProtectedSetting(key)) throw new ValidationException("该设置不允许读取");
                    return await settingProvide.GetStringAsync(key);
                })
            .WithDescription("获取设置").WithDisplayName("获取设置")
            .WithTags("设置");

        setting.MapPost("{key}",
                async (SettingProvide settingProvide, ConfigurationService configService, string key,
                    SettingInput input) =>
                {
                    if (RiskSigningKey.IsManagedSetting(key) || RiskSigningKey.IsManagedSetting(input.Key))
                        throw new ValidationException("该设置不允许通过此接口修改");

                    if (input.Key == LogRetention.SettingKey &&
                        (!int.TryParse(input.Value, out var days) || !LogRetention.IsAllowed(days)))
                        throw new ValidationException("日志保留天数仅支持 1、7、15、30 天");

                    if (input.Key == AdminLoginGuard.ClientIpSourceSettingKey &&
                        (!Enum.TryParse<ClientIpSource>(input.Value, out var ipSource) ||
                         !Enum.IsDefined(ipSource)))
                        throw new ValidationException("不支持的客户端 IP 来源");

                    await settingProvide.SetAsync(input.Key, input.Value);

                    if (input.Key == LogRetention.SettingKey) LogRetention.Refresh(configService);
                })
            .WithDescription("设置设置").WithDisplayName("设置设置")
            .WithTags("设置");

        return app;
    }
}