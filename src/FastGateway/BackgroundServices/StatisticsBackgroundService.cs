using System.Collections.Concurrent;
using System.Threading.Channels;
using IP2Region.Net.Abstractions;

namespace FastGateway.BackgroundServices;

public sealed class StatisticsBackgroundService(IServiceProvider serviceProvider, ISearcher searcher)
    : BackgroundService
{
    private static readonly Channel<StatisticRequestCountDto> StatisticRequestChannel =
        Channel.CreateUnbounded<StatisticRequestCountDto>();

    private static readonly ConcurrentDictionary<string, StatisticRequestCountDto> RequestCountDic = new(-1, 5);
    private static readonly ConcurrentDictionary<string, StatisticIpDto> IpDic = new(-1,5);

    private static readonly Channel<StatisticIpDto> StatisticIpChannel = Channel.CreateUnbounded<StatisticIpDto>();

    private static DateTime _statisticLastTime = DateTime.Now;
    private static DateTime _statisticIpLastTime = DateTime.Now;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Factory.StartNew(RequestCountAsync, stoppingToken);
        await Task.Factory.StartNew(StatisticIpAsync, stoppingToken);
    }

    public static void Write(StatisticRequestCountDto requestCount)
    {
        StatisticRequestChannel.Writer.TryWrite(requestCount);
    }

    public static void Write(StatisticIpDto ipDto)
    {
        StatisticIpChannel.Writer.TryWrite(ipDto);
    }

    private async Task StatisticIpAsync()
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var freeSql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
        while (await StatisticIpChannel.Reader.WaitToReadAsync())
        {
            var ipDto = await StatisticIpChannel.Reader.ReadAsync();

            // 判断是否超过5分钟,如果超过5分钟就将之前的数据写入数据库
            if (DateTime.Now - _statisticIpLastTime >= TimeSpan.FromMinutes(5))
            {
                _statisticIpLastTime = DateTime.Now;

                foreach (var item in IpDic)
                {
                    await CheckAddOrUpdateAsync(freeSql, item.Value);
                }

                IpDic.Clear();
            }

            // 判断是否存在缓存
            if (IpDic.TryGetValue(ipDto.Ip, out var statisticIpDto))
            {
                statisticIpDto.Count += 1;
            }
            else
            {
                IpDic.TryAdd(ipDto.Ip, ipDto);

                var locations = searcher.Search(ipDto.Ip).Split("|", StringSplitOptions.RemoveEmptyEntries);
                // 删除0
                locations = locations.Where(x => x != "0").ToArray();
                if (locations.Length == 0)
                {
                    ipDto.Location = "未知";
                    continue;
                }

                // 然后去掉最后一个
                var location = locations.Distinct().ToArray();

                ipDto.Location = string.Join("", location)
                    .Replace("电信", "")
                    .Replace("联通", "")
                    .Replace("移动", "");
            }
        }
    }

    private async Task RequestCountAsync()
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var freeSql = scope.ServiceProvider.GetRequiredService<IFreeSql>();
        while (await StatisticRequestChannel.Reader.WaitToReadAsync())
        {
            var requestCount = await StatisticRequestChannel.Reader.ReadAsync();

            // 判断是否超过5分钟,如果超过5分钟就将之前的数据写入数据库
            if (DateTime.Now - _statisticLastTime >= TimeSpan.FromMinutes(5))
            {
                _statisticLastTime = DateTime.Now;
                foreach (var item in RequestCountDic)
                {
                    await CheckAddOrUpdateAsync(freeSql, item.Value);
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

    private static async ValueTask CheckAddOrUpdateAsync(IFreeSql freeSql,
        StatisticIpDto requestCountDto)
    {
        var year = (ushort)requestCountDto.CreatedTime.Year;
        var month = (byte)requestCountDto.CreatedTime.Month;
        var day = (byte)requestCountDto.CreatedTime.Day;
        // 判断数据库是否存在今年今月今日的数据
        if (await freeSql.Select<StatisticIp>()
                .AnyAsync(x => x.ServiceId == requestCountDto.ServiceId
                               && x.Ip == requestCountDto.Ip
                               && x.Year == year
                               && x.Month == month
                               && x.Day == day))
        {
            // 更新
            await freeSql.Update<StatisticIp>()
                .Where(x => x.ServiceId == requestCountDto.ServiceId
                            && x.Year == year
                            && x.Month == month
                            && x.Day == day)
                .Set(y => y.Count + requestCountDto.Count)
                .ExecuteAffrowsAsync();
        }
        else
        {
            // 直接添加
            await freeSql.Insert(new StatisticIp()
                {
                    ServiceId = requestCountDto.ServiceId,
                    Count = requestCountDto.Count,
                    Year = year,
                    Month = month,
                    Day = day,
                    Ip = requestCountDto.Ip,
                    Location = requestCountDto.Location,
                })
                .ExecuteAffrowsAsync();
        }
    }

    private static async ValueTask CheckAddOrUpdateAsync(IFreeSql freeSql,
        StatisticRequestCountDto requestCountDto)
    {
        var year = (ushort)requestCountDto.CreatedTime.Year;
        var month = (byte)requestCountDto.CreatedTime.Month;
        var day = (byte)requestCountDto.CreatedTime.Day;
        
        // 判断数据库是否存在今年今月今日的数据
        if (await freeSql.Select<StatisticRequestCount>()
                .AnyAsync(x => x.ServiceId == requestCountDto.ServiceId
                               && x.Year == year
                               && x.Month == month
                               && x.Day == day))
        {
            // 更新
            await freeSql.Update<StatisticRequestCount>()
                    .Where(x => x.ServiceId == requestCountDto.ServiceId
                                && x.Year == year
                                && x.Month == month
                                && x.Day == day)
                    .Set(x => x.Error5xxCount + requestCountDto.Error5xxCount)
                    .Set(x => x.Error4xxCount + requestCountDto.Error4xxCount)
                    .Set(x => x.RequestCount + requestCountDto.RequestCount)
                    .ExecuteAffrowsAsync()
                ;
        }
        else
        {
            // 直接添加
            await freeSql.Insert(new StatisticRequestCount
            {
                ServiceId = requestCountDto.ServiceId,
                RequestCount = requestCountDto.RequestCount,
                Error4xxCount = requestCountDto.Error4xxCount,
                Error5xxCount = requestCountDto.Error5xxCount,
                Year = year,
                Month = month,
                Day = day
            }).ExecuteAffrowsAsync();
        }
    }
}