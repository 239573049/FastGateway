using FastGateway.Service.DataAccess;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.BackgroundTask;

public class LogCleaningBackgroundService(ILogger<LogCleaningBackgroundService> logger,IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 暂停1分钟
        await Task.Delay(1000 * 60, stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 暂停一天
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
                
                await using var scope = serviceProvider.CreateAsyncScope();
                
                var loggerContext = scope.ServiceProvider.GetRequiredService<LoggerContext>();
                
                // 清理掉一个月前的日志
                var date = DateTime.Now.AddMonths(-1);
                var count = await loggerContext.ApplicationLoggers
                    .Where(x => x.RequestTime < date)
                    .ExecuteDeleteAsync(cancellationToken: stoppingToken);
                
                logger.LogInformation($"清理日志：{count}");
            }
            catch (Exception e)
            {
                logger.LogError(e, "日志清理失败");
            }
        }
    }
}