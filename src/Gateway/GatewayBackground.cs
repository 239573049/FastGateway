namespace Gateway;

public class GatewayBackgroundService(GatewayService gatewayService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 添加测试数据
        var cluster = new ClusterEntity()
        {
            ClusterId = Guid.NewGuid().ToString("N"),
            ClusterName = "测试集群",
            Description = "用于测试集群功能",
            DestinationsEntities =
            [
                new()
                {
                    Address = "https://cn.bing.com"
                }
            ]
        };
        await gatewayService.CreateClusterAsync(cluster);

        var router = new RouteEntity()
        {
            ClusterId = cluster.ClusterId,
            RouteId = Guid.NewGuid().ToString("N"),
            RouteName = "测试路由",
            Description = "用于测试路由功能",
            MatchEntities = new RouteMatchEntity()
            {
                Path = "{**catch-all}"
            }
        };

        await gatewayService.CreateRouteAsync(router);

        // 首次启动时更新配置
        await gatewayService.UpdateConfig();
    }
}