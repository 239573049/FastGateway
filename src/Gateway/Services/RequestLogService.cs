using System.Text.Json;
using System.Threading.Channels;
using Gateway.Dto;
using Gateway.Entities;
using Microsoft.Extensions.Caching.Memory;

namespace Gateway.Services;

/// <summary>
/// 请求日志服务处理
/// </summary>
public class RequestLogService
{
    private readonly Channel<RequestLog> _channel;
    private readonly IFreeSql _freeSql;
    private readonly IMemoryCache _memoryCache;

    public RequestLogService(IFreeSql freeSql, IMemoryCache memoryCache)
    {
        _freeSql = freeSql;
        _memoryCache = memoryCache;
        _channel = Channel.CreateUnbounded<RequestLog>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        });

        Task.Run(HandleAsync);
    }

    private async Task HandleAsync()
    {
        while (await _channel.Reader.WaitToReadAsync())
        {
            while (_channel.Reader.TryRead(out var requestLog))
            {
                await _freeSql.Insert(requestLog).ExecuteAffrowsAsync();
            }
        }
    }

    public async ValueTask LogAsync(RequestLog requestLog)
    {
        await _channel.Writer.WriteAsync(requestLog);
    }

    public async Task<List<RequestLogPanel>> PanelAsync(int hours)
    {
        // 从缓存中获取
        var cacheKey = $"RequestLogPanel-{hours}";
        if (_memoryCache.TryGetValue(cacheKey, out List<RequestLogPanel> result))
            return result;
        
        // hours是传递的多少小时，查询每小时的请求量，返回一个数组
        result = (await _freeSql.Ado.QueryAsync<RequestLogPanel>(
            $"""
             WITH RECURSIVE HoursBefore AS (
                 SELECT strftime('%Y-%m-%d %H:00:00', 'now', 'localtime') AS StartTime,
                        strftime('%Y-%m-%d %H:59:59', 'now', 'localtime') AS EndTime
                 UNION ALL
                 SELECT datetime(StartTime, '-1 hour') AS StartTime,
                        datetime(EndTime, '-1 hour') AS EndTime
                 FROM HoursBefore
                 WHERE StartTime > datetime('now', 'localtime', '-' || {hours} || ' hours')
             )
             SELECT
                 HB.StartTime AS StartTime,
                 COUNT(Log.Id) AS Value
             FROM
                 HoursBefore HB
                     LEFT JOIN
                 (SELECT * FROM RequestLog WHERE CreatedTime BETWEEN datetime('now', 'localtime', '-' || {hours} || ' hours') AND datetime('now', 'localtime')) AS Log
                 ON
                     Log.CreatedTime >= HB.StartTime
                         AND Log.CreatedTime <= HB.EndTime
             GROUP BY
                 HB.StartTime
             ORDER BY
                 HB.StartTime DESC
             """)).OrderBy(x => x.StartTime).ToList();

        // 设置缓存
        _memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(30));
        
        return result;
    }
}