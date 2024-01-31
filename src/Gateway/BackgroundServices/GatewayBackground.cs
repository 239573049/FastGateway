using Gateway.Middlewares.FlowAnalytics;

namespace Gateway.BackgroundServices;

public class GatewayBackgroundService(
    GatewayService gatewayService,
    CertificateService certificateService,
    IFlowAnalyzer flowAnalyzer,
    IFreeSql freeSql) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 数据库迁移
        freeSql.CodeFirst.SyncStructure(typeof(RouteEntity), typeof(ClusterEntity), typeof(CertificateEntity),
            typeof(StaticFileProxyEntity), typeof(SystemLoggerEntity));

        // 首次启动时更新配置
        await gatewayService.RefreshConfig();
        await certificateService.RefreshConfig();

        while (stoppingToken.IsCancellationRequested == false)
        {
            // 获取当前时间距离23:59:59剩余多少秒，然后等待这么多秒
            var seconds = (DateTime.Now.Date.AddDays(1) - DateTime.Now).TotalSeconds;

            // Task.Delay不能是负数
            if (seconds < 0)
            {
                seconds = 1000;
            }

            #region 等待第二天凌晨

            // 如果服务手动被关闭，则先统计当天流量记录。
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);

                // 停止一秒防止时间计算错误
                await Task.Delay(1000, stoppingToken);
            }
            catch
            {
            }

            #endregion

            var flow = flowAnalyzer.GetFlowStatistics();

            var systemLoggerEntity = new SystemLoggerEntity
            {
                Id = Guid.NewGuid().ToString("N"),
                RequestCount = GatewayMiddleware.CurrentRequestCount,
                ErrorRequestCount = GatewayMiddleware.CurrentErrorCount,
                CurrentTime = DateTime.Now.AddDays(-1),
                ReadRate = flow.TotalRead,
                WriteRate = flow.TotalWrite
            };

            // 清空请求计数器
            GatewayMiddleware.ClearRequestCount();
            flowAnalyzer.CleanRecords();

            await freeSql.Insert(systemLoggerEntity).ExecuteAffrowsAsync();
        }
    }
}