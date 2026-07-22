using System.ComponentModel.DataAnnotations;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Gateway;
using FastGateway.Infrastructure;

namespace FastGateway.Services;

public static class StreamForwardService
{
    public static IEndpointRouteBuilder MapStreamForward(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/stream-forward")
            .WithTags("端口转发")
            .WithDescription("TCP/UDP 端口转发管理")
            .AddEndpointFilter<ResultFilter>()
            .RequireAuthorization()
            .WithDisplayName("端口转发");

        group.MapPost(string.Empty, (ConfigurationService configService, StreamForward streamForward) =>
        {
            Validate(configService, streamForward, null);
            configService.AddStreamForward(streamForward);
        }).WithDescription("创建端口转发").WithDisplayName("创建端口转发").WithTags("端口转发");

        group.MapGet(string.Empty, (ConfigurationService configService) =>
            {
                return configService.GetStreamForwards().Select(x =>
                {
                    var (online, activeConnections, udpSessions) = StreamProxyManager.GetRuntimeStats(x.Id);
                    return new StreamForwardDto
                    {
                        Id = x.Id,
                        Name = x.Name,
                        Description = x.Description,
                        Enable = x.Enable,
                        Protocol = x.Protocol,
                        ListenPort = x.ListenPort,
                        ListenAddress = x.ListenAddress,
                        UpStreams = x.UpStreams,
                        LoadBalancing = x.LoadBalancing,
                        ConnectTimeoutMs = x.ConnectTimeoutMs,
                        IdleTimeoutSeconds = x.IdleTimeoutSeconds,
                        EnableBlacklist = x.EnableBlacklist,
                        EnableWhitelist = x.EnableWhitelist,
                        OnLine = online,
                        ActiveConnections = activeConnections,
                        UdpSessions = udpSessions
                    };
                }).ToList();
            })
            .WithDescription("获取端口转发列表")
            .WithDisplayName("获取端口转发列表")
            .WithTags("端口转发");

        group.MapGet("{id}/stats", (string id) => StreamProxyManager.GetStats(id))
            .WithDescription("获取端口转发运行状态")
            .WithDisplayName("获取端口转发运行状态")
            .WithTags("端口转发");

        group.MapPut("{id}", (ConfigurationService configService, string id, StreamForward streamForward) =>
        {
            streamForward.Id = id;
            Validate(configService, streamForward, id);
            configService.UpdateStreamForward(streamForward);
        }).WithDescription("更新端口转发").WithDisplayName("更新端口转发").WithTags("端口转发");

        group.MapDelete("{id}", async (ConfigurationService configService, string id) =>
        {
            configService.DeleteStreamForward(id);
            await StreamProxyManager.StopAsync(id);
        }).WithDescription("删除端口转发").WithDisplayName("删除端口转发").WithTags("端口转发");

        group.MapPut("{id}/enable", (ConfigurationService configService, string id) =>
        {
            var streamForward = configService.GetStreamForward(id);
            if (streamForward != null)
            {
                streamForward.Enable = !streamForward.Enable;
                configService.UpdateStreamForward(streamForward);
            }
        }).WithDescription("启用/禁用端口转发").WithDisplayName("启用/禁用端口转发").WithTags("端口转发");

        // 在线启停
        group.MapPut("{id}/online", async (ConfigurationService configService, string id) =>
        {
            if (!StreamProxyManager.CheckOnline(id))
            {
                var streamForward = configService.GetStreamForward(id);
                if (streamForward == null) throw new ValidationException("端口转发不存在");
                await StreamProxyManager.StartAsync(streamForward);
            }
            else
            {
                await StreamProxyManager.StopAsync(id);
            }
        }).WithDescription("启动/停止端口转发").WithDisplayName("启动/停止端口转发").WithTags("端口转发");

        // 重载（重建监听）
        group.MapPut("{id}/reload", async (ConfigurationService configService, string id) =>
        {
            var streamForward = configService.GetStreamForward(id);
            if (streamForward == null) throw new ValidationException("端口转发不存在");

            await StreamProxyManager.ReloadAsync(streamForward);
        }).WithDescription("重载端口转发").WithDisplayName("重载端口转发").WithTags("端口转发");

        return app;
    }

    private static void Validate(ConfigurationService configService, StreamForward streamForward, string? excludeId)
    {
        if (string.IsNullOrWhiteSpace(streamForward.Name)) throw new ValidationException("名称不能为空");

        if (streamForward.ListenPort == 0) throw new ValidationException("监听端口不能为空");

        if (streamForward.UpStreams == null || streamForward.UpStreams.Count == 0)
            throw new ValidationException("至少需要配置一个上游目标");

        foreach (var up in streamForward.UpStreams)
        {
            if (string.IsNullOrWhiteSpace(up.Host)) throw new ValidationException("上游地址不能为空");
            if (up.Port is <= 0 or > 65535) throw new ValidationException("上游端口范围为 1-65535");
        }

        // 端口冲突校验：不与其它转发规则、也不与 HTTP 服务端口冲突
        var conflictWithForward = configService.GetStreamForwards()
            .Any(x => x.Id != excludeId && x.ListenPort == streamForward.ListenPort);
        if (conflictWithForward)
            throw new ValidationException($"端口 {streamForward.ListenPort} 已被其它转发规则占用");

        var conflictWithServer = configService.GetServers()
            .Any(x => x.Listen == streamForward.ListenPort);
        if (conflictWithServer)
            throw new ValidationException($"端口 {streamForward.ListenPort} 已被 HTTP 服务占用");
    }
}
