using FastGateway.Risk.Model;
using FastGateway.Services;

namespace FastGateway.BackgroundTask;

public sealed class JevThreatBackgroundService(
    JevThreatDetectionService detectionService,
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
            try
            {
                var settings = RiskModelProviderStore.Current;
                if (settings is { HttpThreatEnabled: true, IsUsable: true })
                {
                    var interval = TimeSpan.FromHours(Math.Clamp(settings.HttpThreatIntervalHours, 1, 168));
                    var latest = detectionService.GetLatestStatus("auto");
                    if (latest == null || latest.StartedAt <= DateTimeOffset.Now - interval)
                    {
                        var result = await detectionService.RunAutoAsync(stoppingToken);
                        if (result != null)
                            logger.LogInformation(
                                "Jev 恶意请求自动判断完成：候选 {Total}，评估 {Evaluated}，封禁 {Blocked}，黑名单 {Name}",
                                result.TotalCandidates, result.EvaluatedIps, result.BlockedIps, result.BlacklistName);
                    }
                }
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
