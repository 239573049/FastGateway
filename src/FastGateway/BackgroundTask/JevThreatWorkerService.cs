using FastGateway.Services;

namespace FastGateway.BackgroundTask;

/// <summary>独立消费数据库中的 Jev 排队任务，生命周期不依赖发起任务的 HTTP 请求。</summary>
public sealed class JevThreatWorkerService(
    JevThreatDetectionService detectionService,
    ILogger<JevThreatWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            detectionService.FailInterruptedRunningJobs();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "恢复 Jev 后台任务状态失败");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var queued = detectionService.ClaimNextQueuedBatch();
                if (queued == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                var result = await detectionService.RunClaimedBatchAsync(queued, stoppingToken);
                logger.LogInformation(
                    "Jev 后台判断完成：{Trigger}，候选 {Total}，评估 {Evaluated}，封禁 {Blocked}，黑名单 {Name}",
                    result.TriggerType,
                    result.TotalCandidates,
                    result.EvaluatedIps,
                    result.BlockedIps,
                    result.BlacklistName);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Jev 后台判断任务失败");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
