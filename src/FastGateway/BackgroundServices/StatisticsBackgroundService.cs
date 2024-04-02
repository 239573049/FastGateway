using System.Collections.Concurrent;
using System.Threading.Channels;

namespace FastGateway.BackgroundServices;

public sealed class StatisticsBackgroundService(IServiceProvider serviceProvider) : BackgroundService
{
    private static readonly Channel<StatisticRequestCountDto> StatisticRequestChannel =
        Channel.CreateUnbounded<StatisticRequestCountDto>();

    private static readonly ConcurrentDictionary<string, StatisticRequestCountDto> RequestCountDic = new();

    private static readonly ConcurrentDictionary<string, StatisticIpDto> IpDic = new();

    private static DateTime _lastTime = DateTime.Now;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Factory.StartNew(RequestCountAsync, stoppingToken);
    }

    public static void WriteAsync(StatisticRequestCountDto requestCount)
    {
        StatisticRequestChannel.Writer.TryWrite(requestCount);
    }

    public static void WriteAsync(StatisticIpDto ipDto)
    {
        // 判断是否存在缓存
        if (IpDic.TryGetValue(ipDto.Ip, out var statisticIpDto))
        {
            statisticIpDto.CreatedTime = ipDto.CreatedTime;
        }
        else
        {
            IpDic.TryAdd(ipDto.Ip, ipDto);
        }
    }

    private async Task RequestCountAsync()
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var masterDbContext = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        while (await StatisticRequestChannel.Reader.WaitToReadAsync())
        {
            var requestCount = await StatisticRequestChannel.Reader.ReadAsync();

            // 判断是否超过5分钟,如果超过5分钟就将之前的数据写入数据库
            if (DateTime.Now - _lastTime >= TimeSpan.FromMinutes(5))
            {
                _lastTime = DateTime.Now;
                foreach (var item in RequestCountDic)
                {
                    await CheckAddOrUpdateAsync(masterDbContext, item.Value);
                }

                RequestCountDic.Clear();
            }

            // 判断是否存在缓存 
            if (RequestCountDic.TryGetValue(requestCount.ServiceId, out var statisticRequestCount))
            {
                statisticRequestCount.RequestCount += requestCount.RequestCount;
                statisticRequestCount.Error4xxCount += requestCount.Error4xxCount;
                statisticRequestCount.Error5xxCount += requestCount.Error5xxCount;
            }
            else
            {
                RequestCountDic.TryAdd(requestCount.ServiceId, requestCount);
            }

        }
    }

    public static StatisticRequestCountDto GetCurrentRequestCount(string? serviceId)
    {
        if (string.IsNullOrWhiteSpace(serviceId))
        {
            return new StatisticRequestCountDto
            {
                RequestCount = RequestCountDic.Values.Sum(x => x.RequestCount),
                Error4xxCount = RequestCountDic.Values.Sum(x => x.Error4xxCount),
                Error5xxCount = RequestCountDic.Values.Sum(x => x.Error5xxCount),
            };
        }

        if (RequestCountDic.TryGetValue(serviceId, out var statisticRequestCount))
        {
            return statisticRequestCount;
        }

        return new StatisticRequestCountDto
        {
            ServiceId = serviceId,
            RequestCount = 0,
            Error4xxCount = 0,
            Error5xxCount = 0
        };
    }

    private static async ValueTask CheckAddOrUpdateAsync(MasterDbContext masterDbContext,
        StatisticRequestCountDto requestCountDto)
    {
        // 判断数据库是否存在今年今月今日的数据
        if (await masterDbContext.StatisticRequestCounts
                .AnyAsync(x => x.ServiceId == requestCountDto.ServiceId
                               && x.Year == requestCountDto.CreatedTime.Year
                               && x.Month == requestCountDto.CreatedTime.Month
                               && x.Day == requestCountDto.CreatedTime.Day))
        {
            // 更新
            await masterDbContext.StatisticRequestCounts
                .Where(x => x.ServiceId == requestCountDto.ServiceId
                            && x.Year == requestCountDto.CreatedTime.Year
                            && x.Month == requestCountDto.CreatedTime.Month
                            && x.Day == requestCountDto.CreatedTime.Day)
                .ExecuteUpdateAsync(x =>
                    x.SetProperty(y => y.RequestCount, y => y.RequestCount + requestCountDto.RequestCount)
                        .SetProperty(y => y.Error4xxCount, y => y.Error4xxCount + requestCountDto.Error4xxCount)
                        .SetProperty(y => y.Error5xxCount, y => y.Error5xxCount + requestCountDto.Error5xxCount));
        }
        else
        {
            // 直接添加
            await masterDbContext.StatisticRequestCounts.AddAsync(new Domain.StatisticRequestCount
            {
                ServiceId = requestCountDto.ServiceId,
                RequestCount = requestCountDto.RequestCount,
                Error4xxCount = requestCountDto.Error4xxCount,
                Error5xxCount = requestCountDto.Error5xxCount,
                Year = (ushort)requestCountDto.CreatedTime.Year,
                Month = (byte)requestCountDto.CreatedTime.Month,
                Day = (byte)requestCountDto.CreatedTime.Day
            });

            await masterDbContext.SaveChangesAsync();
        }
    }
}