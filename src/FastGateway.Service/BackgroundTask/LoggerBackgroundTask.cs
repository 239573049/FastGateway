using System.Collections.Concurrent;
using System.Threading.Channels;
using FastGateway.Entities;
using FastGateway.Service.DataAccess;
using IP2Region.Net.Abstractions;

namespace FastGateway.Service.BackgroundTask;

public sealed class LoggerBackgroundTask(IServiceProvider serviceProvider, ISearcher searcher) : BackgroundService
{
    private static Channel<ApplicationLogger> LoggerChannel { get; } = Channel.CreateUnbounded<ApplicationLogger>();

    public static void AddLogger(ApplicationLogger logger)
    {
        LoggerChannel.Writer.TryWrite(logger);
    }

    /// <summary>
    /// 线程安全集合
    /// </summary>
    private readonly ConcurrentBag<ApplicationLogger> _loggerBag = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = Start(stoppingToken);
        await RunLoggerSave(stoppingToken);
    }

    private async Task RunLoggerSave(CancellationToken stoppingToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var masterContext = scope.ServiceProvider.GetRequiredService<MasterContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<LoggerBackgroundTask>>();

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_loggerBag.IsEmpty)
            {
                // 等待5s
                await Task.Delay(5000, stoppingToken);
                continue;
            }

            var loggerList = new List<ApplicationLogger>();
            while (_loggerBag.TryTake(out var loggerItem))
            {
                loggerList.Add(loggerItem);
            }

            try
            {
                foreach (var item in loggerList)
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

                await masterContext.ApplicationLoggers.AddRangeAsync(loggerList, stoppingToken);
                await masterContext.SaveChangesAsync(stoppingToken);
            }
            catch (Exception e)
            {
                logger.LogError(e, "日志保存失败");
            }
        }
    }

    private async Task Start(CancellationToken stoppingToken)
    {
        await foreach (var item in LoggerChannel.Reader.ReadAllAsync(stoppingToken))
        {
            _loggerBag.Add(item);
        }
    }
}