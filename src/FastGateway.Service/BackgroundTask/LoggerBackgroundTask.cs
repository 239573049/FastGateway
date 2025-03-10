using System.Threading.Channels;
using FastGateway.Entities;
using FastGateway.Service.DataAccess;
using IP2Region.Net.Abstractions;

namespace FastGateway.Service.BackgroundTask;

public sealed class LoggerBackgroundTask(
    IServiceProvider serviceProvider,
    ISearcher searcher,
    IConfiguration configuration) : BackgroundService
{
    /// <summary>
    /// 线程安全集合
    /// </summary>
    private static readonly Channel<ApplicationLogger> LoggerBag = Channel.CreateUnbounded<ApplicationLogger>();

    private static bool _isRunning = false;

    public static void AddLogger(ApplicationLogger logger)
    {
        if (!_isRunning)
        {
            return;
        }

        LoggerBag.Writer.TryWrite(logger);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (configuration["EnableLog"] == "false")
            return;

        _isRunning = true;
        await RunLoggerSave(stoppingToken);
    }

    private async Task RunLoggerSave(CancellationToken stoppingToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        await using var loggerContext = scope.ServiceProvider.GetRequiredService<LoggerContext>();

        int count = 0;
        var loggers = new List<ApplicationLogger>();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var item = await LoggerBag.Reader.ReadAsync(stoppingToken);

                if (string.IsNullOrWhiteSpace(item.Ip))
                    continue;

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

                loggers.Add(item);
                count++;
                if (count >= 50)
                {
                    await loggerContext.ApplicationLoggers.AddRangeAsync(loggers, stoppingToken);
                    await loggerContext.SaveChangesAsync(stoppingToken);
                    loggers.Clear();
                    count = 0;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await Task.Delay(500, stoppingToken);
            }
        }
    }
}