using System.Collections.Concurrent;
using FastGateway.Entities;
using FastGateway.Service.DataAccess;
using FastGateway.Service.Dto;
using IP2Region.Net.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.BackgroundTask;

public class ClientRequestBackgroundTask(IServiceProvider serviceProvider, ISearcher searcher) : BackgroundService
{
    /// <summary>
    /// 线程安全集合
    /// </summary>
    private static readonly ConcurrentBag<ClientRequestLoggerInput> LoggerBag = new();


    public static void AddLogger(ClientRequestLoggerInput input)
    {
        LoggerBag.Add(input);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 暂停1分钟
        await Task.Delay(1000 * 60, stoppingToken);
        await RunLoggerSave(stoppingToken);
    }

    private async Task RunLoggerSave(CancellationToken stoppingToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var loggerContext = scope.ServiceProvider.GetRequiredService<LoggerContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LoggerBackgroundTask>>();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (LoggerBag.IsEmpty)
            {
                await Task.Delay(10000, stoppingToken);
                continue;
            }

            var loggerList = new List<ClientRequestLoggerInput>();
            while (LoggerBag.TryTake(out var loggerItem))
            {
                loggerList.Add(loggerItem);
            }

            if (loggerList.Count == 0)
                continue;

            try
            {
                var list = new Dictionary<string, ClientRequestLogger>();
                foreach (var input in loggerList)
                {
                    if (list.TryGetValue(input.Ip, out var value))
                    {
                        value.Total++;
                        if (input.Success)
                        {
                            value.Success++;
                        }
                        else
                        {
                            value.Fail++;
                        }
                    }
                    else
                    {
                        list.Add(input.Ip, new ClientRequestLogger()
                        {
                            Ip = input.Ip,
                            Total = 1,
                            Success = input.Success ? 1 : 0,
                            Fail = input.Success ? 0 : 1,
                            RequestTime = DateTime.Now.ToString("yyyy-MM-dd")
                        });
                    }
                }

                var now = DateTime.Now.ToString("yyyy-MM-dd");

                var ips = list.Keys.ToList();

                var existIps = await loggerContext.ClientRequestLoggers
                    .Where(x => x.RequestTime == now && ips.Contains(x.Ip))
                    .Select(x => x.Ip)
                    .ToListAsync(stoppingToken);

                var newIps = ips.Except(existIps).ToList();

                var newLoggerList = list.Where(x => newIps.Contains(x.Key)).Select(x => x.Value).ToList();


                foreach (var item in newLoggerList)
                {
                    if (string.IsNullOrWhiteSpace(item.Ip)) continue;
                    var locations = searcher.Search(item.Ip)?.Split("|", StringSplitOptions.RemoveEmptyEntries);

                    locations = locations?.Where(x => x != "0").ToArray();
                    if (locations == null || locations?.Length == 0)
                    {
                        continue;
                    }

                    item.Country = locations?.First();
                    item.Region = string.Join("|", locations)
                        .Replace("电信", "")
                        .Replace("联通", "")
                        .Replace("移动", "")
                        .TrimStart('|')
                        .TrimEnd('|');
                }

                var updateLoggerList = list.Where(x => existIps.Contains(x.Key)).Select(x => x.Value).ToList();

                foreach (var item in updateLoggerList)
                {
                    await loggerContext.ClientRequestLoggers.Where(x => x.Ip == item.Ip && x.RequestTime == now)
                        .ExecuteUpdateAsync(x => x.SetProperty(a => a.Total, a => a.Total + item.Total)
                                .SetProperty(a => a.Success, a => a.Success + item.Success)
                                .SetProperty(a => a.Fail, a => a.Fail),
                            cancellationToken: stoppingToken);
                }

                await loggerContext.ClientRequestLoggers.AddRangeAsync(newLoggerList, stoppingToken);

                await loggerContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "日志保存失败");
            }
        }
    }
}