using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using FastGateway.Dto;
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
            // 安全加固（本地补丁）：本组的 version / info / resources 原先均可匿名访问，
            // 会泄露内部主机名、进程 ID、启动时间、.NET 与 YARP 版本、实时连接数等信息。
            // 与其余 15 个管理接口保持一致，改为必须携带有效 JWT 才能访问。
            .RequireAuthorization()
            .WithDisplayName("系统");

        system.MapGet("version", GetVersion)
            .WithDescription("获取系统版本信息")
            .WithDisplayName("获取系统版本信息")
            .WithTags("系统");

        system.MapGet("info", GetInfo)
            .WithDescription("获取网关版本与运行信息")
            .WithDisplayName("获取网关版本与运行信息")
            .WithTags("系统");

        system.MapGet("resources", GetResources)
            .WithDescription("获取网关资源运行快照")
            .WithDisplayName("获取网关资源运行快照")
            .WithTags("系统");

        return app;
    }

    private static SystemVersionDto GetVersion()
    {
        // 获取程序集版本号
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "未知版本";

        return new SystemVersionDto
        {
            Version = version,
            Framework = Environment.Version.ToString(),
            Os = Environment.OSVersion.ToString()
        };
    }

    private static SystemResourceDto GetResources()
    {
        return new SystemResourceDto
        {
            ActiveHttpRequests = GatewayResourceMetrics.ActiveHttpRequests,
            ActiveControlConnections = GatewayResourceMetrics.ActiveControlConnections,
            ActiveHttpTunnels = GatewayResourceMetrics.ActiveHttpTunnels,
            ActiveWebSockets = GatewayResourceMetrics.ActiveWebSockets,
            ActiveTcpConnections = GatewayResourceMetrics.ActiveTcpConnections,
            ActiveUdpSessions = GatewayResourceMetrics.ActiveUdpSessions,
            TcpRejected = GatewayResourceMetrics.TcpRejected,
            UdpRejected = GatewayResourceMetrics.UdpRejected,
            FailoverRequests = GatewayResourceMetrics.FailoverRequests,
            FailoverAttempts = GatewayResourceMetrics.FailoverAttempts,
            FailoverRetries = GatewayResourceMetrics.FailoverRetries,
            FailoverExhausted = GatewayResourceMetrics.FailoverExhausted,
            CheckedAtUtc = DateTime.UtcNow
        };
    }

    private static SystemInfoDto GetInfo()
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

        return new SystemInfoDto
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
