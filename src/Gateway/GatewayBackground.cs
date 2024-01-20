namespace Gateway;

public class GatewayBackgroundService(GatewayService gatewayService,CertificateService certificateService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 首次启动时更新配置
        await gatewayService.RefreshConfig();
        await certificateService.RefreshConfig();
    }
}