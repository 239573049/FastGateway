namespace FastGateway.Services;

public sealed class StatisticRequestService : ServiceBase
{
    /// <summary>
    /// 获取今天的统计数据
    /// </summary>
    public async Task<StatisticRequestCountDto> GetTodayStatisticAsync(MasterDbContext masterDbContext,
        string? serviceId)
    {
        var now = DateTime.Now;
        var today = new DateTime(now.Year, now.Month, now.Day);

        var statistic = await masterDbContext.StatisticRequestCounts
            .Where(x => (string.IsNullOrEmpty(serviceId) || x.ServiceId == serviceId) && x.Year == today.Year &&
                        x.Month == today.Month &&
                        x.Day == today.Day)
            .FirstOrDefaultAsync();

        if (statistic == null)
        {
            return new StatisticRequestCountDto();
        }

        return new StatisticRequestCountDto
        {
            RequestCount = statistic.RequestCount,
            Error4xxCount = statistic.Error4xxCount,
            Error5xxCount = statistic.Error5xxCount,
            ServiceId = statistic.ServiceId,
            CreatedTime = new DateTime(statistic.Year, statistic.Month, statistic.Day, 0, 0, 0)
        };
    }
}