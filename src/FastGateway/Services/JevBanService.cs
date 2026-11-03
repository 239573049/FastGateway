using FastGateway.Risk.Model;
using FastGateway.Services.Statistics;
using Microsoft.Data.Sqlite;
using static FastGateway.Services.Statistics.SqlRunner;

namespace FastGateway.Services;

/// <summary>
///     「Jev 自动封禁」分组的生命周期：按次递增的封禁时长、到期自动解封、手动解封后的豁免期。
///     配置分组是实际生效的封禁来源（请求路径只查分组缓存）；jev_ban 记录时长与历史，<see cref="Sweep" /> 负责两者对账，
///     因此在黑白名单页面直接编辑该分组也会被识别：移除视为手动解封，手动加入的 IP 同样按 TTL 到期。
/// </summary>
public sealed class JevBanService(ConfigurationService configService, ILogger<JevBanService> logger)
{
    public const string BlacklistGroupName = "Jev 自动封禁";

    private const string BlacklistGroupDescription = "由 Jev 恶意请求判断自动加入，按配置时长到期自动解封";
    private static readonly string[] GroupNames = [BlacklistGroupName];

    // 封禁、解封与对账都要同时读写配置分组和 jev_ban，串行化避免对账把刚加入的 IP 误判为「未登记」
    private readonly Lock _lock = new();

    /// <summary>
    ///     旧版每个批次新建一个 "jev-yyyyMMdd-HHmmss-自动/人工" 分组；启动时合并进 <see cref="BlacklistGroupName" />。
    ///     合并进来的 IP 在下一次 <see cref="Sweep" /> 时登记为首次封禁并开始计时。
    /// </summary>
    public static int MigrateLegacyBlacklistGroups(ConfigurationService configService)
    {
        return configService.MergeBlacklistGroups(
            x => x.Name != null && JevThreatDetectionService.IsLegacyBlacklistName(x.Name) &&
                 (x.Regions?.Count ?? 0) == 0,
            BlacklistGroupName,
            BlacklistGroupDescription);
    }

    /// <summary>把 IP 加入 Jev 分组并登记封禁时长。</summary>
    /// <returns>IP 是否为本次新加入</returns>
    public bool Ban(SqliteConnection connection, string ip, double risk, string batchId,
        RiskModelProviderSettings settings)
    {
        lock (_lock)
        {
            if (!configService.AddIpToBlacklistGroup(BlacklistGroupName, BlacklistGroupDescription, ip)) return false;
            BlacklistAndWhitelistService.RefreshCache(configService);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var offenses = ExecuteScalarInt(connection, "SELECT offense_count FROM jev_ban WHERE ip = @ip",
                parameters: [new P("@ip", ip)]) + 1;
            var expiresAt = ExpiresAt(settings, offenses, now);
            Execute(connection,
                """
                INSERT INTO jev_ban (ip, offense_count, active, banned_at, expires_at, last_batch_id, last_risk)
                VALUES (@ip, @offenses, 1, @now, @expires, @batch, @risk)
                ON CONFLICT(ip) DO UPDATE SET
                    offense_count = excluded.offense_count,
                    active = 1,
                    banned_at = excluded.banned_at,
                    expires_at = excluded.expires_at,
                    last_batch_id = excluded.last_batch_id,
                    last_risk = excluded.last_risk,
                    released_at = NULL,
                    release_reason = NULL,
                    exempt_until = NULL
                """,
                parameters:
                [
                    new P("@ip", ip), new P("@offenses", offenses), new P("@now", now),
                    new P("@expires", expiresAt), new P("@batch", batchId), new P("@risk", risk)
                ]);

            logger.LogInformation("Jev 封禁 IP {Ip}（风险 {Risk:P0}，第 {Offenses} 次，{Duration}），批次 {BatchId}",
                ip, risk, offenses, DescribeExpiry(expiresAt, now), batchId);
            return true;
        }
    }

    /// <summary>
    ///     手动解封：从 Jev 分组移除，并按配置进入豁免期（豁免期内 Jev 不再评估该 IP）。
    /// </summary>
    /// <returns>从分组中移除的条目数</returns>
    public int Unban(string ip)
    {
        lock (_lock)
        {
            var removed = configService.RemoveIpFromBlacklistGroups(GroupNames, ip);
            if (removed == 0) return 0;
            BlacklistAndWhitelistService.RefreshCache(configService);

            try
            {
                StatisticsDb.Initialize();
                if (StatisticsDb.IsAvailable)
                {
                    using var connection = StatisticsDb.OpenWriteConnection();
                    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    MarkManuallyReleased(connection, null, ip, now, RiskModelProviderStore.Current);
                }
            }
            catch (Exception ex)
            {
                // 分组已移除即解封生效；豁免记录写失败只影响后续是否重新评估
                logger.LogError(ex, "记录 Jev 解封豁免失败，IP {Ip}", ip);
            }

            return removed;
        }
    }

    /// <summary>当前处于豁免期的 IP。</summary>
    public static HashSet<string> GetExemptIps(SqliteConnection connection)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return Query(connection, "SELECT ip FROM jev_ban WHERE exempt_until > @now", r => r.GetString(0),
                parameters: [new P("@now", now)])
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     对账配置分组与 jev_ban：到期的移出分组；已不在分组里的视为手动解封；分组里未登记的 IP 登记并开始计时。
    /// </summary>
    public void Sweep()
    {
        StatisticsDb.Initialize();
        if (!StatisticsDb.IsAvailable) return;

        var settings = RiskModelProviderStore.Current;
        lock (_lock)
        {
            var groupIps = configService.GetBlacklistAndWhitelists()
                .Where(x => x.IsBlacklist &&
                            string.Equals(x.Name, BlacklistGroupName, StringComparison.OrdinalIgnoreCase))
                .SelectMany(x => x.Ips ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            using var connection = StatisticsDb.OpenWriteConnection();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var active = Query(connection, "SELECT ip, expires_at FROM jev_ban WHERE active = 1",
                r => (Ip: r.GetString(0), ExpiresAt: r.IsDBNull(1) ? (long?)null : r.GetInt64(1)));
            var activeIps = active.Select(x => x.Ip).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var expired = active.Where(x => x.ExpiresAt <= now && groupIps.Contains(x.Ip)).Select(x => x.Ip).ToList();
            var removedManually = active.Where(x => !groupIps.Contains(x.Ip)).Select(x => x.Ip).ToList();
            var untracked = groupIps.Where(x => !activeIps.Contains(x)).ToList();
            if (expired.Count + removedManually.Count + untracked.Count == 0) return;

            // 先移出分组再写库：中途失败时下一轮会把这些 IP 识别为「手动移除」，至多多给一段豁免期，不会漏解封
            if (expired.Count > 0)
            {
                configService.RemoveIpsFromBlacklistGroups(GroupNames, expired);
                BlacklistAndWhitelistService.RefreshCache(configService);
            }

            using var transaction = connection.BeginTransaction();
            foreach (var ip in expired)
                Execute(connection,
                    """
                    UPDATE jev_ban SET active = 0, released_at = @now, release_reason = 'expired'
                    WHERE ip = @ip
                    """,
                    transaction,
                    new P("@ip", ip), new P("@now", now));

            foreach (var ip in removedManually)
                MarkManuallyReleased(connection, transaction, ip, now, settings);

            foreach (var ip in untracked)
            {
                var offenses = ExecuteScalarInt(connection, "SELECT offense_count FROM jev_ban WHERE ip = @ip",
                    transaction, new P("@ip", ip)) + 1;
                Execute(connection,
                    """
                    INSERT INTO jev_ban (ip, offense_count, active, banned_at, expires_at)
                    VALUES (@ip, @offenses, 1, @now, @expires)
                    ON CONFLICT(ip) DO UPDATE SET
                        offense_count = excluded.offense_count,
                        active = 1,
                        banned_at = excluded.banned_at,
                        expires_at = excluded.expires_at,
                        released_at = NULL,
                        release_reason = NULL,
                        exempt_until = NULL
                    """,
                    transaction,
                    new P("@ip", ip), new P("@offenses", offenses), new P("@now", now),
                    new P("@expires", ExpiresAt(settings, offenses, now)));
            }

            transaction.Commit();

            if (expired.Count > 0) logger.LogInformation("Jev 封禁到期自动解封 {Count} 个 IP", expired.Count);
            if (removedManually.Count > 0)
                logger.LogInformation("检测到 {Count} 个 IP 已被手动移出 Jev 分组，记为解封并进入豁免期", removedManually.Count);
            if (untracked.Count > 0)
                logger.LogInformation("Jev 分组中有 {Count} 个未登记 IP，已登记并开始计时", untracked.Count);
        }
    }

    private static void MarkManuallyReleased(SqliteConnection connection, SqliteTransaction? transaction, string ip,
        long now, RiskModelProviderSettings settings)
    {
        long? exemptUntil = settings.HttpThreatExemptDays > 0
            ? now + (long)TimeSpan.FromDays(settings.HttpThreatExemptDays).TotalSeconds
            : null;
        Execute(connection,
            """
            INSERT INTO jev_ban (ip, active, released_at, release_reason, exempt_until)
            VALUES (@ip, 0, @now, 'manual', @exempt)
            ON CONFLICT(ip) DO UPDATE SET
                active = 0,
                released_at = excluded.released_at,
                release_reason = excluded.release_reason,
                exempt_until = excluded.exempt_until
            """,
            transaction,
            new P("@ip", ip), new P("@now", now), new P("@exempt", exemptUntil));
    }

    /// <summary>首次 BanHours，之后每次翻倍，封顶 BanMaxHours；BanHours 为 0 表示永久（返回 null）。</summary>
    private static long? ExpiresAt(RiskModelProviderSettings settings, int offenses, long now)
    {
        if (settings.HttpThreatBanHours <= 0) return null;
        var hours = Math.Min(settings.HttpThreatBanMaxHours,
            settings.HttpThreatBanHours * Math.Pow(2, Math.Clamp(offenses - 1, 0, 20)));
        return now + (long)TimeSpan.FromHours(hours).TotalSeconds;
    }

    private static string DescribeExpiry(long? expiresAt, long now)
    {
        return expiresAt == null ? "永久" : $"{TimeSpan.FromSeconds(expiresAt.Value - now).TotalHours:0.#} 小时";
    }
}
