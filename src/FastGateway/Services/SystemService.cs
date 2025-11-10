using System.Reflection;
using FastGateway.Infrastructure;

namespace FastGateway.Services;

public static class SystemService
{
    public static IEndpointRouteBuilder MapSystem(this IEndpointRouteBuilder app)
    {
        var system = app.MapGroup("/api/v1/system")
            .WithTags("系统")
            .WithDescription("系统信息管理")
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("系统");

        system.MapGet("version", GetVersion)
            .WithDescription("获取系统版本信息")
            .WithDisplayName("获取系统版本信息")
            .WithTags("系统");

        return app;
    }

    private static object GetVersion()
    {
        // 获取程序集版本号
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "未知版本";
        

        return new
        {
            Version = version,
            Framework = Environment.Version.ToString(),
            Os = Environment.OSVersion.ToString()
        };
    }
}
