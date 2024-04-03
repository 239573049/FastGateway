namespace FastGateway.Services;

public static class StatisticRequestService
{
    /// <summary>
    /// 获取今天的统计数据
    /// </summary>
    public static async Task<ResultDto<StatisticRequestCountDto>> GetDayStatisticAsync(MasterDbContext masterDbContext,
        string? serviceId)
    {
        var now = DateTime.Now;
        var today = new DateTime(now.Year, now.Month, now.Day);

        var statistic = await masterDbContext.StatisticRequestCounts
            .Where(x => (string.IsNullOrEmpty(serviceId) || x.ServiceId == serviceId) && x.Year == today.Year &&
                        x.Month == today.Month &&
                        x.Day == today.Day)
            .FirstOrDefaultAsync();

        var currentStatistic = StatisticsBackgroundService.GetCurrentRequestCount(serviceId);

        if (statistic == null)
        {
            return ResultDto<StatisticRequestCountDto>.SuccessResult(new StatisticRequestCountDto()
            {
                Error4xxCount = currentStatistic.Error4xxCount,
                Error5xxCount = currentStatistic.Error5xxCount,
                RequestCount = currentStatistic.RequestCount
            });
        }

        return ResultDto<StatisticRequestCountDto>.SuccessResult(new StatisticRequestCountDto
        {
            RequestCount = statistic.RequestCount + currentStatistic.RequestCount,
            Error4xxCount = statistic.Error4xxCount + currentStatistic.Error4xxCount,
            Error5xxCount = statistic.Error5xxCount + currentStatistic.Error5xxCount,
        });
    }

    public static async Task<ResultDto<StatisticRequestCountDto>> GetTotalStatisticAsync(
        MasterDbContext masterDbContext,
        string? serviceId)
    {
        var query = masterDbContext.StatisticRequestCounts.Where(x =>
            string.IsNullOrEmpty(serviceId) || x.ServiceId == serviceId);

        var currentStatistic = StatisticsBackgroundService.GetCurrentRequestCount(serviceId);

        var requestCount = await query.SumAsync(x => x.RequestCount);

        var error4XxCount = await query.SumAsync(x => x.Error4xxCount);

        var error5XxCount = await query.SumAsync(x => x.Error5xxCount);

        return ResultDto<StatisticRequestCountDto>.SuccessResult(new StatisticRequestCountDto
        {
            RequestCount = requestCount + currentStatistic.RequestCount,
            Error4xxCount = error4XxCount + currentStatistic.Error4xxCount,
            Error5xxCount = error5XxCount + currentStatistic.Error5xxCount,
        });
    }

    public static async Task<List<StatisticLocationDto>> GetStatisticLocationAsync(MasterDbContext masterDbContext)
    {
        var query = from ip in masterDbContext.StatisticIps
            group ip by ip.Location
            into g
            orderby g.Sum(x => x.Count) descending
            select new StatisticLocationDto
            {
                Location = g.Key,
                Count = g.Sum(x => x.Count)
            };


        var result = await query
            .Take(10)
            .ToListAsync();

        // 计算比例
        var total = result.Sum(x => x.Count);
        result.ForEach(x => x.Ratio = Math.Round(((double)x.Count / total) * 100, 2));

        return result;
    }
}