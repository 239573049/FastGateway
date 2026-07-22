using System.Text;
using FastGateway.Services.Statistics;
using Microsoft.Data.Sqlite;
using static FastGateway.Services.Statistics.SqlRunner;

namespace FastGateway.BackgroundTask;

/// <summary>
///     统计后台消费者：单读者批量消费采集通道，完成 geo/UA 富化后在单事务内
///     批量写入明细、分钟/小时聚合桶、维度聚合与每日唯一集合，并负责保留期清理。
/// </summary>
public sealed class StatisticsBackgroundService(ILogger<StatisticsBackgroundService> logger) : BackgroundService
{
    private const int BatchSize = 500;
    private const int BatchDelayMs = 1000;
    private const int RawRetentionDays = 7;
    private const int AggRetentionDays = 90;
    private const int DimKeyLimitPerBucket = 300;
    private const string DimOverflowKey = "__other__";
    private const int MetaFlushBatches = 60;
    private const string GeoZhBackfillMetaKey = "geo_zh_country_v1_completed";

    private static readonly int[] UnboundedDimTypes = [StatDimType.RefererHost, StatDimType.RefererUrl, StatDimType.Path];

    // 无界维度基数限制追踪（当前小时内 (bucket,host,type) 已见 key 集合）
    private readonly Dictionary<(long Bucket, string Host, int Type), HashSet<string>> _dimKeyTracker = new();

    // 当日唯一集合（day → (host,kind) → 已写入哈希），避免重复写库
    private readonly Dictionary<int, Dictionary<(string Host, int Kind), HashSet<long>>> _uniqueSeen = new();

    private long _lastCleanupTs;
    private int _batchesSinceMetaFlush;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        StatisticsDb.Initialize(logger);
        GeoIpService.Initialize(logger);

        if (!StatisticsDb.IsAvailable)
        {
            logger.LogWarning("统计数据库不可用，统计后台服务未启动");
            return;
        }

        BackfillGeoLocalization();
        logger.LogInformation("统计后台服务已启动");

        SqliteConnection? connection = null;
        var batch = new List<RequestStatEntry>(BatchSize);

        try
        {
            connection = StatisticsDb.OpenWriteConnection();
            _lastCleanupTs = ReadMetaLong(connection, "last_cleanup_ts");

            var reader = StatisticsCollector.Reader;

            while (!stoppingToken.IsCancellationRequested)
            {
                if (!await reader.WaitToReadAsync(stoppingToken)) break;

                batch.Clear();
                while (batch.Count < BatchSize && reader.TryRead(out var entry)) batch.Add(entry);

                // 未凑满一批则等一个批处理周期再补齐，摊薄事务开销
                if (batch.Count < BatchSize)
                {
                    try
                    {
                        await Task.Delay(BatchDelayMs, stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        // 停机前落盘已读取的数据
                    }

                    while (batch.Count < BatchSize && reader.TryRead(out var entry)) batch.Add(entry);
                }

                if (batch.Count == 0) continue;

                try
                {
                    WriteBatch(connection, batch);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "统计批量写入失败，丢弃 {Count} 条", batch.Count);
                }

                if (++_batchesSinceMetaFlush >= MetaFlushBatches)
                {
                    _batchesSinceMetaFlush = 0;
                    TryFlushMeta(connection);
                }

                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (now - _lastCleanupTs >= 3600)
                {
                    _lastCleanupTs = now;
                    TryCleanup(connection, now);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常停机
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "统计后台服务异常退出");
        }
        finally
        {
            connection?.Dispose();
        }
    }

    private void BackfillGeoLocalization()
    {
        if (!GeoIpService.IsReady)
        {
            logger.LogWarning("ip2region.xdb 不可用，跳过地理统计回填");
            return;
        }

        using var connection = StatisticsDb.OpenWriteConnection();
        var completed = ExecuteScalarString(connection,
            "SELECT value FROM stat_meta WHERE key = @key", parameters: [new P("@key", GeoZhBackfillMetaKey)]);
        if (completed == "1") return;

        var startTs = DateTimeOffset.UtcNow.AddDays(-RawRetentionDays).ToUnixTimeSeconds();
        var bucketStart = startTs / 3600 * 3600;
        var rows = Query(connection,
            "SELECT id AS Id, ip AS Ip FROM request_log WHERE ts >= @startTs",
            r => new GeoBackfillRow { Id = r.GetLong(0), Ip = r.GetStringOrEmpty(1) },
            parameters: [new P("@startTs", startTs)]);

        using var transaction = connection.BeginTransaction();
        foreach (var row in rows)
        {
            var (country, province) = GeoIpService.Resolve(row.Ip);
            Execute(connection,
                "UPDATE request_log SET country = @country, province = @province WHERE id = @id",
                transaction,
                new P("@country", country), new P("@province", province), new P("@id", row.Id));
        }

        // 旧聚合里可能残留英文国名；超过 7 天且无 IP 的记录仅做名称归一
        var englishCountries = Query(connection,
            """
            SELECT DISTINCT dim_key FROM stat_dim
            WHERE dim_type = @dimType AND dim_key GLOB '*[A-Za-z]*'
            """,
            r => r.GetStringOrEmpty(0),
            transaction, new P("@dimType", StatDimType.Country));
        foreach (var oldName in englishCountries)
        {
            var zh = GeoIpService.NormalizeCountryName(oldName);
            if (zh == oldName) continue;
            Execute(connection,
                """
                UPDATE stat_dim
                SET dim_key = @zh
                WHERE dim_type = @dimType AND dim_key = @oldName
                  AND NOT EXISTS (
                      SELECT 1 FROM stat_dim x
                      WHERE x.bucket = stat_dim.bucket AND x.host = stat_dim.host
                        AND x.dim_type = stat_dim.dim_type AND x.dim_key = @zh)
                """,
                transaction,
                new P("@zh", zh), new P("@oldName", oldName), new P("@dimType", StatDimType.Country));
            Execute(connection,
                """
                UPDATE stat_dim
                SET cnt = cnt + (
                        SELECT COALESCE(SUM(s.cnt), 0) FROM stat_dim s
                        WHERE s.bucket = stat_dim.bucket AND s.host = stat_dim.host
                          AND s.dim_type = stat_dim.dim_type AND s.dim_key = @oldName),
                    blocked = blocked + (
                        SELECT COALESCE(SUM(s.blocked), 0) FROM stat_dim s
                        WHERE s.bucket = stat_dim.bucket AND s.host = stat_dim.host
                          AND s.dim_type = stat_dim.dim_type AND s.dim_key = @oldName)
                WHERE dim_type = @dimType AND dim_key = @zh
                """,
                transaction,
                new P("@zh", zh), new P("@oldName", oldName), new P("@dimType", StatDimType.Country));
            Execute(connection,
                "DELETE FROM stat_dim WHERE dim_type = @dimType AND dim_key = @oldName",
                transaction,
                new P("@dimType", StatDimType.Country), new P("@oldName", oldName));
        }

        Execute(connection,
            """
            DELETE FROM stat_dim
            WHERE (dim_type = @countryType OR dim_type = @provinceType)
              AND bucket >= @bucketStart
            """,
            transaction,
            new P("@countryType", StatDimType.Country),
            new P("@provinceType", StatDimType.Province),
            new P("@bucketStart", bucketStart));
        Execute(connection,
            """
            INSERT INTO stat_dim (bucket, host, dim_type, dim_key, cnt, blocked)
            SELECT ts / 3600 * 3600, host, @countryType, country, COUNT(*),
                   SUM(CASE WHEN blocked != 0 THEN 1 ELSE 0 END)
            FROM request_log
            WHERE ts >= @startTs AND country != ''
            GROUP BY ts / 3600 * 3600, host, country
            UNION ALL
            SELECT ts / 3600 * 3600, '', @countryType, country, COUNT(*),
                   SUM(CASE WHEN blocked != 0 THEN 1 ELSE 0 END)
            FROM request_log
            WHERE ts >= @startTs AND country != ''
            GROUP BY ts / 3600 * 3600, country
            UNION ALL
            SELECT ts / 3600 * 3600, host, @provinceType, province, COUNT(*),
                   SUM(CASE WHEN blocked != 0 THEN 1 ELSE 0 END)
            FROM request_log
            WHERE ts >= @startTs AND country = @china AND province != ''
            GROUP BY ts / 3600 * 3600, host, province
            UNION ALL
            SELECT ts / 3600 * 3600, '', @provinceType, province, COUNT(*),
                   SUM(CASE WHEN blocked != 0 THEN 1 ELSE 0 END)
            FROM request_log
            WHERE ts >= @startTs AND country = @china AND province != ''
            GROUP BY ts / 3600 * 3600, province
            """,
            transaction,
            new P("@countryType", StatDimType.Country),
            new P("@provinceType", StatDimType.Province),
            new P("@startTs", startTs),
            new P("@china", GeoIpService.China));
        Execute(connection,
            "INSERT INTO stat_meta (key, value) VALUES (@key, '1') ON CONFLICT (key) DO UPDATE SET value = excluded.value",
            transaction,
            new P("@key", GeoZhBackfillMetaKey));
        transaction.Commit();

        logger.LogInformation("已回填 {Count} 条最近 {Days} 天的中文地理统计", rows.Count, RawRetentionDays);
    }

    private void WriteBatch(SqliteConnection connection, List<RequestStatEntry> batch)
    {
        var rows = new List<RequestLogRow>(batch.Count);
        var buckets = new Dictionary<(int Granularity, long Bucket, string Host), BucketAgg>();
        var dims = new Dictionary<(long Bucket, string Host, int Type, string Key), DimAgg>();
        var uniques = new List<UniqueRow>();

        foreach (var entry in batch) Accumulate(entry, rows, buckets, dims, uniques);

        using var transaction = connection.BeginTransaction();

        ExecuteBatch(connection,
            """
            INSERT INTO request_log
                (ts, server_id, host, path, method, status, elapsed_ms, ip, country, province,
                 os, browser, visitor_hash, referer_host, referer_url, blocked, is_page)
            VALUES
                (@Ts, @ServerId, @Host, @Path, @Method, @Status, @ElapsedMs, @Ip, @Country, @Province,
                 @Os, @Browser, @VisitorHash, @RefererHost, @RefererUrl, @Blocked, @IsPage)
            """, transaction, rows, static (cmd, row) =>
            {
                cmd.Parameters.AddWithValue("@Ts", row.Ts);
                cmd.Parameters.AddWithValue("@ServerId", (object?)row.ServerId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Host", row.Host);
                cmd.Parameters.AddWithValue("@Path", row.Path);
                cmd.Parameters.AddWithValue("@Method", row.Method);
                cmd.Parameters.AddWithValue("@Status", row.Status);
                cmd.Parameters.AddWithValue("@ElapsedMs", row.ElapsedMs);
                cmd.Parameters.AddWithValue("@Ip", row.Ip);
                cmd.Parameters.AddWithValue("@Country", row.Country);
                cmd.Parameters.AddWithValue("@Province", row.Province);
                cmd.Parameters.AddWithValue("@Os", row.Os);
                cmd.Parameters.AddWithValue("@Browser", row.Browser);
                cmd.Parameters.AddWithValue("@VisitorHash", row.VisitorHash);
                cmd.Parameters.AddWithValue("@RefererHost", row.RefererHost);
                cmd.Parameters.AddWithValue("@RefererUrl", row.RefererUrl);
                cmd.Parameters.AddWithValue("@Blocked", row.Blocked);
                cmd.Parameters.AddWithValue("@IsPage", row.IsPage);
            });

        ExecuteBatch(connection,
            """
            INSERT INTO stat_bucket
                (granularity, bucket, host, requests, page_views, blocked, blocked_403, blocked_429,
                 status_2xx, status_3xx, status_4xx, status_5xx, elapsed_sum)
            VALUES
                (@Granularity, @Bucket, @Host, @Requests, @PageViews, @Blocked, @Blocked403, @Blocked429,
                 @Status2xx, @Status3xx, @Status4xx, @Status5xx, @ElapsedSum)
            ON CONFLICT (granularity, bucket, host) DO UPDATE SET
                requests    = requests    + excluded.requests,
                page_views  = page_views  + excluded.page_views,
                blocked     = blocked     + excluded.blocked,
                blocked_403 = blocked_403 + excluded.blocked_403,
                blocked_429 = blocked_429 + excluded.blocked_429,
                status_2xx  = status_2xx  + excluded.status_2xx,
                status_3xx  = status_3xx  + excluded.status_3xx,
                status_4xx  = status_4xx  + excluded.status_4xx,
                status_5xx  = status_5xx  + excluded.status_5xx,
                elapsed_sum = elapsed_sum + excluded.elapsed_sum
            """, transaction, buckets, static (cmd, pair) =>
            {
                cmd.Parameters.AddWithValue("@Granularity", pair.Key.Granularity);
                cmd.Parameters.AddWithValue("@Bucket", pair.Key.Bucket);
                cmd.Parameters.AddWithValue("@Host", pair.Key.Host);
                cmd.Parameters.AddWithValue("@Requests", pair.Value.Requests);
                cmd.Parameters.AddWithValue("@PageViews", pair.Value.PageViews);
                cmd.Parameters.AddWithValue("@Blocked", pair.Value.Blocked);
                cmd.Parameters.AddWithValue("@Blocked403", pair.Value.Blocked403);
                cmd.Parameters.AddWithValue("@Blocked429", pair.Value.Blocked429);
                cmd.Parameters.AddWithValue("@Status2xx", pair.Value.Status2xx);
                cmd.Parameters.AddWithValue("@Status3xx", pair.Value.Status3xx);
                cmd.Parameters.AddWithValue("@Status4xx", pair.Value.Status4xx);
                cmd.Parameters.AddWithValue("@Status5xx", pair.Value.Status5xx);
                cmd.Parameters.AddWithValue("@ElapsedSum", pair.Value.ElapsedSum);
            });

        ExecuteBatch(connection,
            """
            INSERT INTO stat_dim (bucket, host, dim_type, dim_key, cnt, blocked)
            VALUES (@Bucket, @Host, @Type, @Key, @Cnt, @Blocked)
            ON CONFLICT (bucket, host, dim_type, dim_key) DO UPDATE SET
                cnt = cnt + excluded.cnt, blocked = blocked + excluded.blocked
            """, transaction, dims, static (cmd, pair) =>
            {
                cmd.Parameters.AddWithValue("@Bucket", pair.Key.Bucket);
                cmd.Parameters.AddWithValue("@Host", pair.Key.Host);
                cmd.Parameters.AddWithValue("@Type", pair.Key.Type);
                cmd.Parameters.AddWithValue("@Key", pair.Key.Key);
                cmd.Parameters.AddWithValue("@Cnt", pair.Value.Cnt);
                cmd.Parameters.AddWithValue("@Blocked", pair.Value.Blocked);
            });

        ExecuteBatch(connection,
            "INSERT OR IGNORE INTO stat_unique_daily (day, host, kind, hash) VALUES (@Day, @Host, @Kind, @Hash)",
            transaction, uniques, static (cmd, u) =>
            {
                cmd.Parameters.AddWithValue("@Day", u.Day);
                cmd.Parameters.AddWithValue("@Host", u.Host);
                cmd.Parameters.AddWithValue("@Kind", u.Kind);
                cmd.Parameters.AddWithValue("@Hash", u.Hash);
            });

        transaction.Commit();
    }

    private void Accumulate(
        in RequestStatEntry entry,
        List<RequestLogRow> rows,
        Dictionary<(int, long, string), BucketAgg> buckets,
        Dictionary<(long, string, int, string), DimAgg> dims,
        List<UniqueRow> uniques)
    {
        var (country, province) = GeoIpService.Resolve(entry.Ip);
        var (os, browser) = UserAgentService.Parse(entry.UserAgent);
        var (refererHost, refererUrl) = ParseReferer(entry.Referer, entry.Host);

        var visitorHash = Hash($"{entry.Ip}|{entry.UserAgent}");
        var isBlocked = entry.Blocked != 0;

        rows.Add(new RequestLogRow
        {
            Ts = entry.Ts,
            ServerId = entry.ServerId,
            Host = entry.Host,
            Path = entry.Path,
            Method = entry.Method,
            Status = entry.Status,
            ElapsedMs = entry.ElapsedMs,
            Ip = entry.Ip,
            Country = country,
            Province = province,
            Os = os,
            Browser = browser,
            VisitorHash = visitorHash,
            RefererHost = refererHost,
            RefererUrl = refererUrl,
            Blocked = (int)entry.Blocked,
            IsPage = entry.IsPage ? 1 : 0
        });

        var minuteBucket = entry.Ts / 60 * 60;
        var hourBucket = entry.Ts / 3600 * 3600;

        foreach (var host in (ReadOnlySpan<string>)[entry.Host, string.Empty])
        {
            AccumulateBucket(buckets, (1, minuteBucket, host), entry, isBlocked);
            AccumulateBucket(buckets, (2, hourBucket, host), entry, isBlocked);

            AccumulateDim(dims, hourBucket, host, StatDimType.Country, country, isBlocked);
            if (province.Length > 0)
                AccumulateDim(dims, hourBucket, host, StatDimType.Province, province, isBlocked);
            AccumulateDim(dims, hourBucket, host, StatDimType.Os, os, isBlocked);
            AccumulateDim(dims, hourBucket, host, StatDimType.Browser, browser, isBlocked);
            AccumulateDim(dims, hourBucket, host, StatDimType.Status, entry.Status.ToString(), isBlocked);
            AccumulateDim(dims, hourBucket, host, StatDimType.Path, entry.Path, isBlocked);
            if (refererHost.Length > 0)
            {
                AccumulateDim(dims, hourBucket, host, StatDimType.RefererHost, refererHost, isBlocked);
                AccumulateDim(dims, hourBucket, host, StatDimType.RefererUrl, refererUrl, isBlocked);
            }

            var day = LocalDay(entry.Ts);
            AccumulateUnique(uniques, day, host, StatUniqueKind.Ip, Hash(entry.Ip));
            AccumulateUnique(uniques, day, host, StatUniqueKind.Visitor, visitorHash);
            if (isBlocked)
                AccumulateUnique(uniques, day, host, StatUniqueKind.BlockedIp, Hash(entry.Ip));
        }
    }

    private static void AccumulateBucket(
        Dictionary<(int, long, string), BucketAgg> buckets,
        (int, long, string) key,
        in RequestStatEntry entry,
        bool isBlocked)
    {
        if (!buckets.TryGetValue(key, out var agg))
        {
            agg = new BucketAgg();
            buckets[key] = agg;
        }

        agg.Requests++;
        if (entry.IsPage) agg.PageViews++;
        if (isBlocked)
        {
            agg.Blocked++;
            if (entry.Blocked == (byte)BlockReason.RateLimit) agg.Blocked429++;
            else agg.Blocked403++;
        }

        switch (entry.Status / 100)
        {
            case 2: agg.Status2xx++; break;
            case 3: agg.Status3xx++; break;
            case 4: agg.Status4xx++; break;
            case 5: agg.Status5xx++; break;
        }

        agg.ElapsedSum += entry.ElapsedMs;
    }

    private void AccumulateDim(
        Dictionary<(long, string, int, string), DimAgg> dims,
        long bucket, string host, int type, string key, bool isBlocked)
    {
        key = LimitDimKey(bucket, host, type, key);

        var dimKey = (bucket, host, type, key);
        if (!dims.TryGetValue(dimKey, out var agg))
        {
            agg = new DimAgg();
            dims[dimKey] = agg;
        }

        agg.Cnt++;
        if (isBlocked) agg.Blocked++;
    }

    /// <summary>
    ///     无界维度（path/referer）限制每小时每 host 最多 300 个 distinct key，溢出并入 __other__。
    /// </summary>
    private string LimitDimKey(long bucket, string host, int type, string key)
    {
        if (Array.IndexOf(UnboundedDimTypes, type) < 0) return key;

        var trackerKey = (bucket, host, type);
        if (!_dimKeyTracker.TryGetValue(trackerKey, out var seen))
        {
            // 新的小时桶出现时清掉过期追踪，控制内存
            if (_dimKeyTracker.Count > 0 && _dimKeyTracker.Keys.First().Bucket != bucket)
                _dimKeyTracker.Clear();

            seen = [];
            _dimKeyTracker[trackerKey] = seen;
        }

        if (seen.Contains(key)) return key;
        if (seen.Count >= DimKeyLimitPerBucket) return DimOverflowKey;

        seen.Add(key);
        return key;
    }

    private void AccumulateUnique(List<UniqueRow> uniques, int day, string host, int kind, long hash)
    {
        if (!_uniqueSeen.TryGetValue(day, out var byHostKind))
        {
            // 只保留当天与前一天（跨零点写入），其余清除
            foreach (var staleDay in _uniqueSeen.Keys.Where(d => d < day - 1).ToArray())
                _uniqueSeen.Remove(staleDay);

            byHostKind = new Dictionary<(string, int), HashSet<long>>();
            _uniqueSeen[day] = byHostKind;
        }

        if (!byHostKind.TryGetValue((host, kind), out var seen))
        {
            seen = [];
            byHostKind[(host, kind)] = seen;
        }

        if (!seen.Add(hash)) return;

        uniques.Add(new UniqueRow(day, host, kind, hash));
    }

    private static (string Host, string Url) ParseReferer(string? referer, string requestHost)
    {
        if (string.IsNullOrEmpty(referer)) return (string.Empty, string.Empty);

        if (!Uri.TryCreate(referer, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return (string.Empty, string.Empty);

        var host = uri.Host.ToLowerInvariant();

        // 仅记录外部来源
        if (string.Equals(host, requestHost, StringComparison.OrdinalIgnoreCase))
            return (string.Empty, string.Empty);

        var url = referer.Length > 512 ? referer[..512] : referer;
        return (host, url);
    }

    private static long Hash(string value)
    {
        return unchecked((long)System.IO.Hashing.XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(value)));
    }

    private static int LocalDay(long ts)
    {
        var local = DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime();
        return local.Year * 10000 + local.Month * 100 + local.Day;
    }

    private static long ReadMetaLong(SqliteConnection connection, string key)
    {
        var value = ExecuteScalarString(connection,
            "SELECT value FROM stat_meta WHERE key = @key", parameters: [new P("@key", key)]);
        return long.TryParse(value, out var result) ? result : 0;
    }

    private void TryFlushMeta(SqliteConnection connection)
    {
        try
        {
            Execute(connection,
                "INSERT INTO stat_meta (key, value) VALUES ('dropped_entries_total', @value) ON CONFLICT (key) DO UPDATE SET value = excluded.value",
                parameters: [new P("@value", StatisticsCollector.DroppedCount.ToString())]);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "统计元数据写入失败");
        }
    }

    private void TryCleanup(SqliteConnection connection, long now)
    {
        try
        {
            var rawCutoff = now - RawRetentionDays * 86400L;
            var aggCutoff = now - AggRetentionDays * 86400L;
            var dayCutoff = LocalDay(aggCutoff);

            // 分批删除明细，保持写事务短小
            while (true)
            {
                var deleted = Execute(connection,
                    "DELETE FROM request_log WHERE id IN (SELECT id FROM request_log WHERE ts < @rawCutoff LIMIT 20000)",
                    parameters: [new P("@rawCutoff", rawCutoff)]);
                if (deleted == 0) break;
            }

            Execute(connection, "DELETE FROM stat_bucket WHERE granularity = 1 AND bucket < @rawCutoff",
                parameters: [new P("@rawCutoff", rawCutoff)]);
            Execute(connection, "DELETE FROM stat_bucket WHERE granularity = 2 AND bucket < @aggCutoff",
                parameters: [new P("@aggCutoff", aggCutoff)]);
            Execute(connection, "DELETE FROM stat_dim WHERE bucket < @aggCutoff",
                parameters: [new P("@aggCutoff", aggCutoff)]);
            Execute(connection, "DELETE FROM stat_unique_daily WHERE day < @dayCutoff",
                parameters: [new P("@dayCutoff", dayCutoff)]);

            Execute(connection,
                "INSERT INTO stat_meta (key, value) VALUES ('last_cleanup_ts', @value) ON CONFLICT (key) DO UPDATE SET value = excluded.value",
                parameters: [new P("@value", now.ToString())]);

            Execute(connection, "PRAGMA incremental_vacuum(2000);");
            Execute(connection, "PRAGMA wal_checkpoint(PASSIVE);");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "统计数据清理失败");
        }
    }

    private sealed class GeoBackfillRow
    {
        public long Id { get; init; }
        public string Ip { get; init; } = string.Empty;
    }

    /// <summary>request_log 单行写入载荷（替代原匿名对象，AOT 安全）。</summary>
    private readonly struct RequestLogRow
    {
        public long Ts { get; init; }
        public string? ServerId { get; init; }
        public string Host { get; init; }
        public string Path { get; init; }
        public string Method { get; init; }
        public int Status { get; init; }
        public int ElapsedMs { get; init; }
        public string Ip { get; init; }
        public string Country { get; init; }
        public string Province { get; init; }
        public string Os { get; init; }
        public string Browser { get; init; }
        public long VisitorHash { get; init; }
        public string RefererHost { get; init; }
        public string RefererUrl { get; init; }
        public int Blocked { get; init; }
        public int IsPage { get; init; }
    }

    /// <summary>stat_unique_daily 单行写入载荷。</summary>
    private readonly record struct UniqueRow(int Day, string Host, int Kind, long Hash);

    private sealed class BucketAgg
    {
        public long Requests;
        public long PageViews;
        public long Blocked;
        public long Blocked403;
        public long Blocked429;
        public long Status2xx;
        public long Status3xx;
        public long Status4xx;
        public long Status5xx;
        public long ElapsedSum;
    }

    private sealed class DimAgg
    {
        public long Cnt;
        public long Blocked;
    }
}
