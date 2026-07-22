using FastGateway.Dto;
using static FastGateway.Services.Statistics.SqlRunner;

namespace FastGateway.Services.Statistics;

/// <summary>
///     统计查询（手写 ADO.NET + 只读连接）。≤7天优先明细/分钟桶精确计算，30天走小时桶与每日唯一集合。
///     不使用 Dapper（其运行时 IL emit 映射与 Native AOT 不兼容），改用显式参数绑定与手写列映射。
/// </summary>
public static class StatisticsQueryService
{
    private sealed record RangeInfo(long StartTs, int Granularity, long StepSeconds, bool UseRaw);

    /// <summary>
    ///     range → 起始时间/桶粒度/序列步长。1h 明细仍在保留期内，直接查明细保证精确与新鲜。
    /// </summary>
    private static RangeInfo ResolveRange(string? range)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return range switch
        {
            "1h" => new RangeInfo(now - 3600, 1, 60, true),
            "7d" => new RangeInfo(now - 7 * 86400L, 2, 3600, false),
            "30d" => new RangeInfo(now - 30 * 86400L, 2, 86400, false),
            _ => new RangeInfo(now - 86400L, 1, 600, false)
        };
    }

    private static long TimeZoneOffsetSeconds =>
        (long)TimeZoneInfo.Local.GetUtcOffset(DateTime.Now).TotalSeconds;

    public static StatisticsOverviewDto GetOverview(string? range, string? host)
    {
        var info = ResolveRange(range);
        var hostFilter = host ?? string.Empty;
        var result = new StatisticsOverviewDto { DroppedEntries = StatisticsCollector.DroppedCount };

        if (!StatisticsDb.IsAvailable) return result;

        using var connection = StatisticsDb.OpenReadConnection();

        var totals = Query(connection,
            """
            SELECT COALESCE(SUM(requests), 0)    AS Requests,
                   COALESCE(SUM(page_views), 0)  AS PageViews,
                   COALESCE(SUM(blocked), 0)     AS Blocked,
                   COALESCE(SUM(blocked_403), 0) AS Blocked403,
                   COALESCE(SUM(blocked_429), 0) AS Blocked429,
                   COALESCE(SUM(status_4xx), 0)  AS Status4xx,
                   COALESCE(SUM(status_5xx), 0)  AS Status5xx,
                   COALESCE(SUM(elapsed_sum), 0) AS ElapsedSum
            FROM stat_bucket
            WHERE granularity = @Granularity AND bucket >= @StartTs AND host = @hostFilter
            """,
            r => new BucketTotals
            {
                Requests = r.GetLong(0),
                PageViews = r.GetLong(1),
                Blocked = r.GetLong(2),
                Blocked403 = r.GetLong(3),
                Blocked429 = r.GetLong(4),
                Status4xx = r.GetLong(5),
                Status5xx = r.GetLong(6),
                ElapsedSum = r.GetLong(7)
            },
            parameters:
            [
                new P("@Granularity", info.Granularity),
                new P("@StartTs", info.StartTs),
                new P("@hostFilter", hostFilter)
            ]).FirstOrDefault();

        if (totals != null)
        {
            result.Requests = totals.Requests;
            result.PageViews = totals.PageViews;
            result.Blocked = totals.Blocked;
            result.Blocked403 = totals.Blocked403;
            result.Blocked429 = totals.Blocked429;
            result.Error4xx = totals.Status4xx;
            result.Error5xx = totals.Status5xx;
            if (totals.Requests > 0)
            {
                result.BlockRate = Math.Round(totals.Blocked * 100.0 / totals.Requests, 2);
                result.Error4xxRate = Math.Round(totals.Status4xx * 100.0 / totals.Requests, 2);
                result.Error5xxRate = Math.Round(totals.Status5xx * 100.0 / totals.Requests, 2);
                result.AvgElapsedMs = totals.ElapsedSum / totals.Requests;
            }
        }

        var hostCondition = hostFilter.Length == 0 ? string.Empty : " AND host = @hostFilter";

        if (range == "30d")
        {
            // 超过明细保留期，用每日唯一集合（仍为精确值）
            var dayStart = LocalDay(info.StartTs);
            result.UniqueIps = ExecuteScalarLong(connection,
                $"SELECT COUNT(DISTINCT hash) FROM stat_unique_daily WHERE day >= @dayStart AND host = @hostFilter AND kind = {StatUniqueKind.Ip}",
                parameters: [new P("@dayStart", dayStart), new P("@hostFilter", hostFilter)]);
            result.UniqueVisitors = ExecuteScalarLong(connection,
                $"SELECT COUNT(DISTINCT hash) FROM stat_unique_daily WHERE day >= @dayStart AND host = @hostFilter AND kind = {StatUniqueKind.Visitor}",
                parameters: [new P("@dayStart", dayStart), new P("@hostFilter", hostFilter)]);
            result.AttackIps = ExecuteScalarLong(connection,
                $"SELECT COUNT(DISTINCT hash) FROM stat_unique_daily WHERE day >= @dayStart AND host = @hostFilter AND kind = {StatUniqueKind.BlockedIp}",
                parameters: [new P("@dayStart", dayStart), new P("@hostFilter", hostFilter)]);
        }
        else
        {
            result.UniqueIps = ExecuteScalarLong(connection,
                $"SELECT COUNT(DISTINCT ip) FROM request_log WHERE ts >= @StartTs{hostCondition}",
                parameters: [new P("@StartTs", info.StartTs), new P("@hostFilter", hostFilter)]);
            result.UniqueVisitors = ExecuteScalarLong(connection,
                $"SELECT COUNT(DISTINCT visitor_hash) FROM request_log WHERE ts >= @StartTs{hostCondition}",
                parameters: [new P("@StartTs", info.StartTs), new P("@hostFilter", hostFilter)]);
            result.AttackIps = ExecuteScalarLong(connection,
                $"SELECT COUNT(DISTINCT ip) FROM request_log WHERE ts >= @StartTs AND blocked != 0{hostCondition}",
                parameters: [new P("@StartTs", info.StartTs), new P("@hostFilter", hostFilter)]);
        }

        result.AbnormalIpsLive = AbnormalIpMonitor.GetAbnormalIps().Count;
        return result;
    }

    public static List<TimeSeriesPointDto> GetTimeSeries(string? range, string? host)
    {
        if (!StatisticsDb.IsAvailable) return [];

        var info = ResolveRange(range);
        var hostFilter = host ?? string.Empty;
        var tzOffset = info.StepSeconds >= 86400 ? TimeZoneOffsetSeconds : 0;

        using var connection = StatisticsDb.OpenReadConnection();

        var points = Query(connection,
            """
            SELECT ((bucket + @tzOffset) / @StepSeconds) * @StepSeconds - @tzOffset AS Time,
                   SUM(requests)   AS Requests,
                   SUM(page_views) AS PageViews,
                   SUM(blocked)    AS Blocked,
                   SUM(status_4xx) AS Error4xx,
                   SUM(status_5xx) AS Error5xx
            FROM stat_bucket
            WHERE granularity = @Granularity AND bucket >= @StartTs AND host = @hostFilter
            GROUP BY Time
            ORDER BY Time
            """,
            r => new TimeSeriesPointDto
            {
                Time = r.GetLong(0),
                Requests = r.GetLong(1),
                PageViews = r.GetLong(2),
                Blocked = r.GetLong(3),
                Error4xx = r.GetLong(4),
                Error5xx = r.GetLong(5)
            },
            parameters:
            [
                new P("@Granularity", info.Granularity),
                new P("@StartTs", info.StartTs),
                new P("@StepSeconds", info.StepSeconds),
                new P("@hostFilter", hostFilter),
                new P("@tzOffset", tzOffset)
            ]).ToDictionary(x => x.Time);

        // 补齐空档，保证序列连续
        var result = new List<TimeSeriesPointDto>();
        var alignedStart = (info.StartTs + tzOffset) / info.StepSeconds * info.StepSeconds - tzOffset;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (var time = alignedStart; time <= now; time += info.StepSeconds)
            result.Add(points.TryGetValue(time, out var point) ? point : new TimeSeriesPointDto { Time = time });

        return result;
    }

    public static GeoResultDto GetGeo(string? range, string? host, string? scope, string? mode, int top)
    {
        if (!StatisticsDb.IsAvailable) return new GeoResultDto();

        var info = ResolveRange(range);
        var hostFilter = host ?? string.Empty;
        var isChina = scope == "china";
        var orderColumn = mode == "blocked" ? "Blocked" : "Count";
        top = Math.Clamp(top <= 0 ? 20 : top, 1, 100);

        using var connection = StatisticsDb.OpenReadConnection();

        List<GeoItemDto> items;
        if (info.UseRaw)
        {
            var column = isChina ? "province" : "country";
            var hostCondition = hostFilter.Length == 0 ? string.Empty : " AND host = @hostFilter";
            var chinaCondition = isChina ? " AND country = '中国' AND province != ''" : string.Empty;
            items = Query(connection,
                $"""
                 SELECT {column} AS Name,
                        COUNT(*) AS Count,
                        SUM(CASE WHEN blocked != 0 THEN 1 ELSE 0 END) AS Blocked
                 FROM request_log
                 WHERE ts >= @StartTs{hostCondition}{chinaCondition}
                 GROUP BY {column}
                 ORDER BY {orderColumn} DESC
                 LIMIT @top
                 """,
                MapGeoItem,
                parameters:
                [
                    new P("@StartTs", info.StartTs),
                    new P("@hostFilter", hostFilter),
                    new P("@top", top)
                ]);
        }
        else
        {
            var dimType = isChina ? StatDimType.Province : StatDimType.Country;
            items = Query(connection,
                $"""
                 SELECT dim_key AS Name, SUM(cnt) AS Count, SUM(blocked) AS Blocked
                 FROM stat_dim
                 WHERE bucket >= @StartTs AND host = @hostFilter AND dim_type = @dimType
                 GROUP BY dim_key
                 ORDER BY {orderColumn} DESC
                 LIMIT @top
                 """,
                MapGeoItem,
                parameters:
                [
                    new P("@StartTs", info.StartTs),
                    new P("@hostFilter", hostFilter),
                    new P("@dimType", dimType),
                    new P("@top", top)
                ]);
        }

        var total = items.Sum(x => mode == "blocked" ? x.Blocked : x.Count);
        foreach (var item in items)
        {
            var value = mode == "blocked" ? item.Blocked : item.Count;
            item.Percent = total > 0 ? Math.Round(value * 100.0 / total, 2) : 0;
        }

        return new GeoResultDto { Items = items, Total = total };
    }

    public static PagingDto<RankingItemDto> GetRankings(
        string? range, string? host, string? type, string? mode, int top, int? page, int? pageSize)
    {
        if (!StatisticsDb.IsAvailable) return new PagingDto<RankingItemDto>();

        var info = ResolveRange(range);
        var hostFilter = host ?? string.Empty;
        var orderColumn = mode == "blocked" ? "Blocked" : "Count";

        var paged = page.HasValue && pageSize.HasValue;
        var take = paged ? Math.Clamp(pageSize!.Value, 1, 100) : Math.Clamp(top <= 0 ? 10 : top, 1, 100);
        var skip = paged ? Math.Max(page!.Value - 1, 0) * take : 0;

        using var connection = StatisticsDb.OpenReadConnection();

        string baseQuery;
        P[] parameters;

        if (type == "host")
        {
            // 受访域名：直接来自 stat_bucket（host='' 是全站汇总行，需排除）
            baseQuery =
                """
                SELECT host AS Key, SUM(requests) AS Count, SUM(blocked) AS Blocked
                FROM stat_bucket
                WHERE granularity = @Granularity AND bucket >= @StartTs AND host != ''
                GROUP BY host
                """;
            parameters =
            [
                new P("@Granularity", info.Granularity),
                new P("@StartTs", info.StartTs),
                new P("@take", take),
                new P("@skip", skip)
            ];
        }
        else if (info.UseRaw)
        {
            var column = type switch
            {
                "path" => "path",
                "referer_host" => "referer_host",
                "referer_url" => "referer_url",
                "os" => "os",
                "browser" => "browser",
                "status" => "status",
                _ => "path"
            };
            var hostCondition = hostFilter.Length == 0 ? string.Empty : " AND host = @hostFilter";
            var notEmptyCondition = column.StartsWith("referer") ? $" AND {column} != ''" : string.Empty;
            baseQuery =
                $"""
                 SELECT CAST({column} AS TEXT) AS Key,
                        COUNT(*) AS Count,
                        SUM(CASE WHEN blocked != 0 THEN 1 ELSE 0 END) AS Blocked
                 FROM request_log
                 WHERE ts >= @StartTs{hostCondition}{notEmptyCondition}
                 GROUP BY {column}
                 """;
            parameters =
            [
                new P("@StartTs", info.StartTs),
                new P("@hostFilter", hostFilter),
                new P("@take", take),
                new P("@skip", skip)
            ];
        }
        else
        {
            var dimType = type switch
            {
                "path" => StatDimType.Path,
                "referer_host" => StatDimType.RefererHost,
                "referer_url" => StatDimType.RefererUrl,
                "os" => StatDimType.Os,
                "browser" => StatDimType.Browser,
                "status" => StatDimType.Status,
                _ => StatDimType.Path
            };
            baseQuery =
                """
                SELECT dim_key AS Key, SUM(cnt) AS Count, SUM(blocked) AS Blocked
                FROM stat_dim
                WHERE bucket >= @StartTs AND host = @hostFilter AND dim_type = @dimType
                GROUP BY dim_key
                """;
            parameters =
            [
                new P("@StartTs", info.StartTs),
                new P("@hostFilter", hostFilter),
                new P("@dimType", dimType),
                new P("@take", take),
                new P("@skip", skip)
            ];
        }

        var total = ExecuteScalarInt(connection, $"SELECT COUNT(*) FROM ({baseQuery})", parameters: parameters);
        var grandTotal = ExecuteScalarLong(connection,
            $"SELECT COALESCE(SUM(Count), 0) FROM ({baseQuery})", parameters: parameters);

        var items = Query(connection,
            $"{baseQuery} ORDER BY {orderColumn} DESC LIMIT @take OFFSET @skip",
            r => new RankingItemDto
            {
                Key = r.GetStringOrEmpty(0),
                Count = r.GetLong(1),
                Blocked = r.GetLong(2)
            },
            parameters: parameters);

        foreach (var item in items)
            item.Percent = grandTotal > 0 ? Math.Round(item.Count * 100.0 / grandTotal, 2) : 0;

        return new PagingDto<RankingItemDto>(total, items);
    }

    public static PagingDto<RequestLogItemDto> GetRequests(
        string? range, string? host, string? ip, int? status, bool? blocked, int page, int pageSize)
    {
        if (!StatisticsDb.IsAvailable) return new PagingDto<RequestLogItemDto>();

        var info = ResolveRange(range);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var conditions = "ts >= @StartTs";
        if (!string.IsNullOrEmpty(host)) conditions += " AND host = @host";
        if (!string.IsNullOrEmpty(ip)) conditions += " AND ip = @ip";
        if (status.HasValue) conditions += " AND status = @status";
        if (blocked == true) conditions += " AND blocked != 0";

        var parameters = new List<P>
        {
            new("@StartTs", info.StartTs),
            new("@take", pageSize),
            new("@skip", (page - 1) * pageSize)
        };
        if (!string.IsNullOrEmpty(host)) parameters.Add(new P("@host", host));
        if (!string.IsNullOrEmpty(ip)) parameters.Add(new P("@ip", ip));
        if (status.HasValue) parameters.Add(new P("@status", status.Value));
        var paramArray = parameters.ToArray();

        using var connection = StatisticsDb.OpenReadConnection();

        var total = ExecuteScalarInt(connection,
            $"SELECT COUNT(*) FROM request_log WHERE {conditions}", parameters: paramArray);

        var items = Query(connection,
            $"""
             SELECT id AS Id, ts AS Ts, host AS Host, path AS Path, method AS Method, status AS Status,
                    elapsed_ms AS ElapsedMs, ip AS Ip, country AS Country, province AS Province,
                    os AS Os, browser AS Browser, referer_url AS RefererUrl, blocked AS Blocked
             FROM request_log
             WHERE {conditions}
             ORDER BY id DESC
             LIMIT @take OFFSET @skip
             """,
            r => new RequestLogItemDto
            {
                Id = r.GetLong(0),
                Ts = r.GetLong(1),
                Host = r.GetStringOrEmpty(2),
                Path = r.GetStringOrEmpty(3),
                Method = r.GetStringOrEmpty(4),
                Status = r.GetInt(5),
                ElapsedMs = r.GetInt(6),
                Ip = r.GetStringOrEmpty(7),
                Country = r.GetStringOrEmpty(8),
                Province = r.GetStringOrEmpty(9),
                Os = r.GetStringOrEmpty(10),
                Browser = r.GetStringOrEmpty(11),
                RefererUrl = r.GetStringOrEmpty(12),
                Blocked = r.GetInt(13)
            },
            parameters: paramArray);

        return new PagingDto<RequestLogItemDto>(total, items);
    }

    private static GeoItemDto MapGeoItem(Microsoft.Data.Sqlite.SqliteDataReader r) => new()
    {
        Name = r.GetStringOrEmpty(0),
        Count = r.GetLong(1),
        Blocked = r.GetLong(2)
    };

    private static int LocalDay(long ts)
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime();
        return local.Year * 10000 + local.Month * 100 + local.Day;
    }

    private sealed class BucketTotals
    {
        public long Requests { get; set; }
        public long PageViews { get; set; }
        public long Blocked { get; set; }
        public long Blocked403 { get; set; }
        public long Blocked429 { get; set; }
        public long Status4xx { get; set; }
        public long Status5xx { get; set; }
        public long ElapsedSum { get; set; }
    }
}
