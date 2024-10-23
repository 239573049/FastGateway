using FastGateway.Service.Dto;
using FastGateway.Service.Infrastructure;

namespace FastGateway.Service.Services;

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
                async (SettingProvide settingProvide) => await settingProvide.GetListAsync())
            .WithDescription("获取设置列表").WithDisplayName("获取设置列表").WithTags("设置");

        setting.MapGet("{key}",
                async (SettingProvide settingProvide, string key) => await settingProvide.GetStringAsync(key))
            .WithDescription("获取设置").WithDisplayName("获取设置")
            .WithTags("设置");

        setting.MapPost("{key}",
                async (SettingProvide settingProvide, SettingInput input) =>
                {
                    await settingProvide.SetAsync(input.Key, input.Value);
                })
            .WithDescription("设置设置").WithDisplayName("设置设置")
            .WithTags("设置");

        return app;
    }
}