using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using FastGateway.Infrastructure;
using Yarp.ReverseProxy.Forwarder;

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

        system.MapGet("info", GetInfo)
            .WithDescription("获取网关版本与运行信息")
            .WithDisplayName("获取网关版本与运行信息")
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

    private static object GetInfo()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var assemblyName = assembly.GetName();

        var version = assemblyName.Version?.ToString() ?? "未知版本";

        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var fileVersion = assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        var description = assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description;
        var company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;

        string? yarpVersion = null;

        try
        {
            yarpVersion = typeof(IForwarderHttpClientFactory).Assembly.GetName().Version?.ToString();
        }
        catch
        {
        }

        var now = DateTime.Now;

        int? processId = null;
        DateTime? processStartTime = null;
        long? uptimeSeconds = null;

        try
        {
            var process = Process.GetCurrentProcess();
            processId = process.Id;
            processStartTime = process.StartTime;

            uptimeSeconds = processStartTime.HasValue
                ? (long)Math.Max(0, (now - processStartTime.Value).TotalSeconds)
                : null;
        }
        catch
        {
        }

        return new
        {
            Name = assemblyName.Name,
            Version = version,
            InformationalVersion = informationalVersion,
            FileVersion = fileVersion,
            Product = product,
            Description = description,
            Company = company,
            YarpVersion = yarpVersion,
            Framework = RuntimeInformation.FrameworkDescription,
            Os = RuntimeInformation.OSDescription,
            OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            MachineName = Environment.MachineName,
            EnvironmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
            ServerTime = now,
            ProcessId = processId,
            ProcessStartTime = processStartTime,
            UptimeSeconds = uptimeSeconds
        };
    }
}
