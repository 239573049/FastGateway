using System.Net.NetworkInformation;

namespace Gateway.BackgroundServices;

public class GatewayBackgroundService(
    GatewayService gatewayService,
    CertificateService certificateService,
    SettingService settingService,
    IFreeSql freeSql) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 数据库迁移
        freeSql.CodeFirst.SyncStructure(typeof(RouteEntity), typeof(ClusterEntity), typeof(CertificateEntity), typeof(StaticFileProxyEntity), typeof(SettingEntity));

        // 首次启动时更新配置
        await gatewayService.RefreshConfig();
        await certificateService.RefreshConfig();

        if (!await settingService.AnyAsync(Constant.Setting.MaxRequestBodySize))
        {
            await settingService.SetAsync(Constant.Setting.MaxRequestBodySize, 1024, "请求最大的Body", "设置请求体的最大大小单位（MB）！");
        }
    }

}