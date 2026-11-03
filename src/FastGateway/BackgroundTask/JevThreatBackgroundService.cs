using FastGateway.Services;

namespace FastGateway.BackgroundTask;

public sealed class JevThreatBackgroundService(
    JevThreatDetectionService detectionService,
    JevBanService banService,
    ILogger<JevThreatBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            // 到期解封与模型是否启用无关：关闭 Jev 后已有封禁也要按时释放
            try
            {
                banService.Sweep();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Jev 封禁到期对账失败");
            }

            try
            {
                var queued = detectionService.QueueAutomatic();
                if (queued != null)
                    logger.LogInformation("Jev 恶意请求自动判断已加入后台队列，批次 {BatchId}", queued.BatchId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Jev 恶意请求自动判断任务失败");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
