using System.Text.Json;
using Core.Entities;
using FastGateway.Gateway;
using FastGateway.Infrastructure;
using FastGateway.Services;

namespace FastGateway.Cluster;

/// <summary>
///     从节点配置应用引擎：接收主网关快照后落盘并按差异调整运行时 ——
///     仅域名变化的服务走 YARP 热重载，服务/限流定义变化的重启对应网关，L4 转发按规则重载。
/// </summary>
public static class ClusterConfigApplier
{
    private static readonly SemaphoreSlim ApplyLock = new(1, 1);

    public static async Task ApplyAsync(ClusterConfigPayload payload, ConfigurationService configService)
    {
        await ApplyLock.WaitAsync();
        try
        {
            var newConfig = payload.Config;

            WriteCertFiles(payload);

            // 差异比对基于应用前的旧配置
            var oldServers = configService.GetServers().ToDictionary(s => s.Id);
            var oldStreamForwards = configService.GetStreamForwards().ToDictionary(s => s.Id);
            var rateLimitsChanged = SerializeRateLimits(configService.GetRateLimits())
                                    != SerializeRateLimits(newConfig.RateLimits);

            configService.ReplaceConfig(newConfig);

            // 证书与黑白名单缓存是全局静态，直接刷新即可生效
            CertService.ReplaceCerts(newConfig.Certs.Where(c => !c.Expired).ToArray());
            BlacklistAndWhitelistService.RefreshCache(newConfig.BlacklistAndWhitelists);

            await ReconcileServersAsync(configService, oldServers, rateLimitsChanged);
            await ReconcileStreamForwardsAsync(configService, oldStreamForwards);
        }
        finally
        {
            ApplyLock.Release();
        }
    }

    /// <summary>把主网关推来的 PFX 写入本地 certs 目录，并把配置中的证书路径改写为本地路径</summary>
    private static void WriteCertFiles(ClusterConfigPayload payload)
    {
        if (payload.CertFiles.Count == 0) return;

        var certDir = Path.Combine(AppContext.BaseDirectory, "certs");
        if (!Directory.Exists(certDir)) Directory.CreateDirectory(certDir);

        var files = payload.CertFiles.ToDictionary(f => f.CertId);

        foreach (var cert in payload.Config.Certs)
        {
            if (!files.TryGetValue(cert.Id, out var file)) continue;

            // 文件名来自主网关下发，只取纯文件名，防止绝对路径或 ../ 写到证书目录之外
            var fileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrEmpty(fileName) || fileName is "." or "..") continue;

            var localPath = Path.Combine(certDir, fileName);
            try
            {
                File.WriteAllBytes(localPath, file.Data);
                cert.Certs.File = localPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"证书 {cert.Domain} 写入失败：{ex.Message}");
            }
        }
    }

    private static async Task ReconcileServersAsync(ConfigurationService configService,
        Dictionary<string, Server> oldServers, bool rateLimitsChanged)
    {
        var newServers = configService.GetServers();
        var newIds = newServers.Select(s => s.Id).ToHashSet();

        // 已被主网关删除的服务
        foreach (var oldId in oldServers.Keys.Where(id => !newIds.Contains(id)))
            if (Gateway.Gateway.CheckServerOnline(oldId))
                await Gateway.Gateway.CloseGateway(oldId);

        var blacklists = configService.GetBlacklistAndWhitelists();
        var rateLimits = configService.GetRateLimits();

        foreach (var server in newServers)
        {
            var online = Gateway.Gateway.CheckServerOnline(server.Id);

            if (!server.Enable)
            {
                if (online) await Gateway.Gateway.CloseGateway(server.Id);
                continue;
            }

            var serverChanged = !oldServers.TryGetValue(server.Id, out var oldServer)
                                || SerializeServer(oldServer) != SerializeServer(server);

            if (online && (serverChanged || rateLimitsChanged))
            {
                // 监听/限流等构建期参数变化，必须重启该网关
                await Gateway.Gateway.CloseGateway(server.Id);
                online = false;
            }

            var domainNames = configService.GetDomainNamesByServerId(server.Id);

            if (online)
            {
                // 仅路由/上游变化：热重载，不断开现有连接
                Gateway.Gateway.ReloadGateway(server, [.. domainNames]);
            }
            else
            {
                await StartServerAsync(server, domainNames, blacklists, rateLimits);
            }
        }
    }

    /// <summary>
    ///     启动网关并有界等待其上线。BuilderGateway 会阻塞到网关停止，只能后台启动；
    ///     但必须等到监听建立（或超时）再返回，否则 ApplyLock 释放后下一次快照会把
    ///     仍在启动中的服务误判为离线而再次启动，造成端口抢占。
    /// </summary>
    private static async Task StartServerAsync(Server server, DomainName[] domainNames,
        List<BlacklistAndWhitelist> blacklists, List<RateLimit> rateLimits)
    {
        _ = Task.Factory.StartNew(async () =>
            await Gateway.Gateway.BuilderGateway(server, domainNames, blacklists, rateLimits));

        for (var i = 0; i < 10; i++)
        {
            if (Gateway.Gateway.CheckServerOnline(server.Id)) return;
            await Task.Delay(500);
        }

        Console.WriteLine($"服务 {server.Name}({server.Listen}) 启动等待超时，可能端口被占用，详见网关启动日志");
    }

    private static async Task ReconcileStreamForwardsAsync(ConfigurationService configService,
        Dictionary<string, StreamForward> oldStreamForwards)
    {
        var newForwards = configService.GetStreamForwards();
        var newIds = newForwards.Select(f => f.Id).ToHashSet();

        foreach (var oldId in oldStreamForwards.Keys.Where(id => !newIds.Contains(id)))
            await StreamProxyManager.StopAsync(oldId);

        foreach (var forward in newForwards)
        {
            var changed = !oldStreamForwards.TryGetValue(forward.Id, out var old)
                          || SerializeStreamForward(old) != SerializeStreamForward(forward);

            // ReloadAsync = Stop + Start，Start 对未启用规则自动忽略，天然覆盖启停两种情况
            if (changed) await StreamProxyManager.ReloadAsync(forward);
        }
    }

    private static string SerializeServer(Server server)
    {
        return JsonSerializer.Serialize(server, AppJsonContext.Default.Server);
    }

    private static string SerializeStreamForward(StreamForward forward)
    {
        return JsonSerializer.Serialize(forward, AppJsonContext.Default.StreamForward);
    }

    private static string SerializeRateLimits(List<RateLimit> rateLimits)
    {
        return JsonSerializer.Serialize(rateLimits, AppJsonContext.Default.ListRateLimit);
    }
}
