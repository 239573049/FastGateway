namespace Gateway.Services;

/// <summary>
/// 请求日志服务处理
/// </summary>
public class RequestLogService
{
    private readonly Channel<RequestLog> _channel;
    private readonly IFreeSql _freeSql;
    private readonly IMemoryCache _memoryCache;
    private readonly SettingService _settingService;

    public RequestLogService(IFreeSql freeSql, IMemoryCache memoryCache, SettingService settingService)
    {
        _freeSql = freeSql;
        _memoryCache = memoryCache;
        _settingService = settingService;
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

    public async Task<int> ClearLogAsync()
    {
        // 获取系统配置
        var day = await _settingService.GetAsync<int>(Constant.Setting.LogRetentionTime);
        
        // 计算时间差
        var startTime = DateTime.Now.AddDays(-day);
        
        // 删除数据
        return await _freeSql.Delete<RequestLog>().Where(x => x.CreatedTime <= startTime).ExecuteAffrowsAsync();
    }

    public async Task<PageResultDto<RequestLog>> GetListAsync(string keyword, int page, int pageSize,
        DateTime? startTime, DateTime? endTime)
    {
        var query = _freeSql.Select<RequestLog>()
            .WhereIf(!string.IsNullOrEmpty(keyword), x => x.Path.Contains(keyword));
        if (startTime != null)
        {
            query = query.Where(x => x.CreatedTime >= startTime);
        }

        if (endTime != null)
        {
            query = query.Where(x => x.CreatedTime <= endTime);
        }

        var total = await query.CountAsync();

        var list = await query
            .OrderByDescending(x => x.CreatedTime)
            .Page(page, pageSize)
            .ToListAsync();

        return new PageResultDto<RequestLog>(total, list);
    }

    public async ValueTask LogAsync(RequestLog requestLog)
    {
        await _channel.Writer.WriteAsync(requestLog);
    }

    public async Task<ResultDto<RequestLogPanel>> PanelAsync(int hours)
    {
        // 从缓存中获取
        var cacheKey = $"RequestLogPanel-{hours}";
        if (_memoryCache.TryGetValue(cacheKey, out RequestLogPanel result))
            return ResultDto<RequestLogPanel>.Success(result);

        result = new RequestLogPanel();

        // hours是传递的多少小时，查询每小时的请求量，返回一个数组
        var requestSizePanels = (await _freeSql.Ado.QueryAsync<RequestSizePanel>(
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

        result.RequestSize = requestSizePanels;
        var pathResult = (await _freeSql.Ado.QueryAsync<RequestPathPanel>(
            "select count(*) as Value, Path AS Type,Id from RequestLog where CreatedTime > @Time group by Path order by count(*) desc limit 5",
            new
            {
                Time = DateTime.Now.ToString("yyyy-MM-dd 00:00")
            })).OrderByDescending(x => x.Value).ToList();

        // 24小时内状态码统计
        var statusCodeResult = (await _freeSql.Ado.QueryAsync<RequestStatusCodePanel>(
            $@"SELECT
    strftime('%Y-%m-%d %H', CreatedTime) as Hour,
    CreatedTime,
    SUM(CASE WHEN StatusCode >= 200 AND StatusCode < 300 THEN 1 ELSE 0 END) as NormalRequests,
    SUM(CASE WHEN StatusCode >= 400 AND StatusCode < 500 THEN 1 ELSE 0 END) as ExceptionalRequests,
    SUM(CASE WHEN StatusCode >= 500 THEN 1 ELSE 0 END) as ErrorRequests
FROM
    RequestLog
WHERE
    CreatedTime >= datetime('now', '-7 days')
GROUP BY
    strftime('%Y-%m-%d %H', CreatedTime)
ORDER BY
    strftime('%Y-%m-%d %H', CreatedTime) ASC;
",
            new
            {
                Time = DateTime.Now.ToString("yyyy-MM-dd 00:00")
            })).OrderBy(x => x.CreatedTime).ToList();

        result.RequestPath = pathResult;

        var requestStatusCode = new List<object>();

        foreach (var statusCodePanel in statusCodeResult)
        {
            requestStatusCode.Add(new
            {
                Type = statusCodePanel.CreatedTime.ToString("yyyy-MM-dd HH"),
                country = "正常请求",
                value = statusCodePanel.NormalRequests
            });

            requestStatusCode.Add(new
            {
                Type = statusCodePanel.CreatedTime.ToString("yyyy-MM-dd HH"),
                country = "特殊请求",
                value = statusCodePanel.ExceptionalRequests
            });

            requestStatusCode.Add(new
            {
                Type = statusCodePanel.CreatedTime.ToString("yyyy-MM-dd HH"),
                country = "异常请求",
                value = statusCodePanel.ErrorRequests
            });
        }

        result.RequestStatusCode = requestStatusCode;

        // 设置缓存
        _memoryCache.Set(cacheKey, result, TimeSpan.FromMinutes(5));

        return ResultDto<RequestLogPanel>.Success(result);
    }
}

public static class RequestLogExtension
{
    public static void MapRequestLog(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gateway/panel", async (RequestLogService requestLogService, int hours) =>
                await requestLogService.PanelAsync(hours))
            .RequireAuthorization();

        app.MapGet("/api/gateway/request-log", async (RequestLogService requestLogService, string keyword, int page,
                    int pageSize,
                    DateTime? startTime, DateTime? endTime) =>
                await requestLogService.GetListAsync(keyword, page, pageSize, startTime, endTime))
            .RequireAuthorization();
    }
}