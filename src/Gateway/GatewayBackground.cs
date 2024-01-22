namespace Gateway;

public class GatewayBackgroundService(
    GatewayService gatewayService,
    CertificateService certificateService,
    IFreeSql freeSql) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 数据库迁移
        freeSql.CodeFirst.SyncStructure(typeof(RouteEntity), typeof(ClusterEntity), typeof(CertificateEntity),
            typeof(RequestLog), typeof(StaticFileProxyEntity), typeof(SettingEntity));

        // 首次启动时更新配置
        await gatewayService.RefreshConfig();
        await certificateService.RefreshConfig();
    }
}