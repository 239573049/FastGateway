using FastGateway.Contract;

namespace FastGateway.BackgroundServices;

/// <summary>
/// 请求来源统计后台服务
/// 统计每天的请求来源，然后获取归属地
/// </summary>
/// <param name="requestSourceService"></param>
/// <param name="freeSql"></param>
/// <param name="logger"></param>
/// <param name="homeAddressService"></param>
public sealed class RequestSourceBackgroundService(
    RequestSourceService requestSourceService,
    IFreeSql freeSql,
    ILogger<RequestSourceBackgroundService> logger,
    IHomeAddressService homeAddressService) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (stoppingToken.IsCancellationRequested == false)
        {
            // 获取当前时间距离00:00:00剩余多少秒，然后等待这么多秒
            var seconds = (DateTime.Now.Date.AddDays(1) - DateTime.Now).TotalSeconds;

            // Task.Delay不能是负数
            if (seconds < 0) seconds = 1000;

            await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken);
            // 获取减1小时时间，以便数据处理
            var nextTime = DateTime.Now.AddHours(-1);

            try
            {
                var result = requestSourceService.GetAndClearDataAsync();

                foreach (var entity in result)
                {
                    if (entity.HomeAddress.IsNullOrEmpty())
                    {
                        entity.HomeAddress = await homeAddressService.GetHomeAddress(entity.Ip).ConfigureAwait(false);
                    }

                    entity.CreatedTime = nextTime;
                }

                if (result?.Count > 0)
                    await freeSql.Insert(result).ExecuteAffrowsAsync();
            }
            catch (Exception e)
            {
                logger.LogError(e, "RequestSourceBackgroundService Error");
            }
        }
    }
}