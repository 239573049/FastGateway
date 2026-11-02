using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using static FastGateway.Services.Statistics.SqlRunner;

namespace FastGateway.Services.Statistics;

/// <summary>
///     统计库（data/stats.db）：建库、PRAGMA 与连接工厂。写连接全局唯一（由后台服务持有），
///     读侧使用短生命周期只读池化连接，WAL 下读写互不阻塞。
/// </summary>
public static class StatisticsDb
{
    private const int SchemaVersion = 3;
    private const int RetryIntervalMs = 5_000;

    private static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "data", "stats.db");

    private static readonly string WriteConnectionString =
        new SqliteConnectionStringBuilder { DataSource = DbPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();

    private static readonly string ReadConnectionString =
        new SqliteConnectionStringBuilder
            { DataSource = DbPath, Mode = SqliteOpenMode.ReadOnly, Cache = SqliteCacheMode.Private }.ToString();

    private static readonly Lock InitLock = new();
    private static long _nextRetryMs;

    public static bool IsAvailable { get; private set; }

    /// <summary>最近一次初始化失败的可读原因，供仪表盘展示。成功时为 null。</summary>
    public static string? UnavailableReason { get; private set; }

    public static void Initialize(ILogger? logger = null)
    {
        if (IsAvailable) return;
        if (Volatile.Read(ref _nextRetryMs) > Environment.TickCount64) return;

        lock (InitLock)
        {
            if (IsAvailable) return;
            if (_nextRetryMs > Environment.TickCount64) return;

            try
            {
                // Native AOT 会裁掉 SQLitePCLRaw 的模块初始化器；必须在 Open 之前显式加载。
                SQLitePCL.Batteries_V2.Init();
                EnsureDataDirectoryWritable();

                using var connection = new SqliteConnection(WriteConnectionString);
                connection.Open();
                Execute(connection, "PRAGMA journal_mode=WAL;");
                Execute(connection, "PRAGMA synchronous=NORMAL;");
                Execute(connection, "PRAGMA auto_vacuum=INCREMENTAL;");
                CreateSchema(connection);
                IsAvailable = true;
                UnavailableReason = null;
            }
            catch (Exception ex)
            {
                // 统计库不可用只降级统计功能，绝不影响代理转发；稍后由后台服务/查询侧重试
                IsAvailable = false;
                UnavailableReason = FormatFailure(ex);
                _nextRetryMs = Environment.TickCount64 + RetryIntervalMs;
                logger?.LogError(ex, "统计数据库初始化失败（{DbPath}），统计功能已降级：{Reason}",
                    DbPath, UnavailableReason);
                if (logger == null)
                    Console.Error.WriteLine($"统计数据库初始化失败（{DbPath}）：{UnavailableReason}");
            }
        }
    }

    private static void EnsureDataDirectoryWritable()
    {
        var directory = Path.GetDirectoryName(DbPath);
        if (string.IsNullOrEmpty(directory))
            throw new InvalidOperationException($"无法解析统计库目录：{DbPath}");

        Directory.CreateDirectory(directory);

        var uid = CurrentUid();
        var probe = Path.Combine(directory, $".stats-write-probe-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllText(probe, "ok");
        }
        catch (Exception ex)
        {
            throw new IOException(
                $"目录不可写：{directory}（进程 UID={uid}）。Docker 镜像以非 root 运行，chmod 给宿主机登录用户不够，请执行：chown -R {uid}:{uid} data",
                ex);
        }
        finally
        {
            try { File.Delete(probe); }
            catch { /* ignore */ }
        }

        if (!File.Exists(DbPath)) return;

        try
        {
            using var fs = new FileStream(DbPath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        }
        catch (Exception ex)
        {
            throw new IOException(
                $"stats.db 已存在但无法写入（进程 UID={uid}）。请在宿主机执行：chown -R {uid}:{uid} data",
                ex);
        }
    }

    private static string FormatFailure(Exception ex)
    {
        var uid = CurrentUid();
        for (var inner = ex; inner != null; inner = inner.InnerException)
        {
            if (inner is DllNotFoundException ||
                inner.Message.Contains("e_sqlite3", StringComparison.OrdinalIgnoreCase) ||
                inner.Message.Contains("libe_sqlite3", StringComparison.OrdinalIgnoreCase))
            {
                return $"无法加载 SQLite 原生库 e_sqlite3（与 data 目录权限无关）。{inner.Message}";
            }
        }

        if (ex is IOException)
            return ex.Message;

        if (ex is SqliteException sqlite && sqlite.SqliteErrorCode is 14 or 8)
        {
            return
                $"无法打开 {DbPath}（进程 UID={uid}，SQLite {sqlite.SqliteErrorCode}: {sqlite.Message}）。请在宿主机执行：chown -R {uid}:{uid} data";
        }

        return $"无法打开 {DbPath}（进程 UID={uid}）：{ex.GetType().Name}: {ex.Message}";
    }

    private static string CurrentUid()
    {
        if (OperatingSystem.IsWindows()) return Environment.UserName;
        try { return Libc.getuid().ToString(); }
        catch { return Environment.UserName; }
    }

    private static class Libc
    {
        [DllImport("libc", ExactSpelling = true)]
        internal static extern uint getuid();
    }

    public static SqliteConnection OpenWriteConnection()
    {
        var connection = new SqliteConnection(WriteConnectionString);
        connection.Open();
        Execute(connection, "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000; PRAGMA temp_store=MEMORY;");
        return connection;
    }

    public static SqliteConnection OpenReadConnection()
    {
        var connection = new SqliteConnection(ReadConnectionString);
        connection.Open();
        Execute(connection, "PRAGMA busy_timeout=3000;");
        return connection;
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        Execute(connection,
            """
            CREATE TABLE IF NOT EXISTS request_log (
                id            INTEGER PRIMARY KEY,
                ts            INTEGER NOT NULL,
                server_id     TEXT    NOT NULL,
                host          TEXT    NOT NULL,
                path          TEXT    NOT NULL,
                method        TEXT    NOT NULL,
                status        INTEGER NOT NULL,
                elapsed_ms    INTEGER NOT NULL,
                ip            TEXT    NOT NULL,
                country       TEXT    NOT NULL,
                province      TEXT    NOT NULL DEFAULT '',
                os            TEXT    NOT NULL,
                browser       TEXT    NOT NULL,
                visitor_hash  INTEGER NOT NULL,
                referer_host  TEXT    NOT NULL DEFAULT '',
                referer_url   TEXT    NOT NULL DEFAULT '',
                blocked       INTEGER NOT NULL DEFAULT 0,
                is_page       INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_request_log_ts      ON request_log (ts);
            CREATE INDEX IF NOT EXISTS idx_request_log_host_ts ON request_log (host, ts);
            -- 部分索引：拦截记录占比极低，安全页"仅看拦截"/攻击 IP 统计走此索引避免全表扫
            CREATE INDEX IF NOT EXISTS idx_request_log_blocked_ts ON request_log (ts) WHERE blocked != 0;

            CREATE TABLE IF NOT EXISTS stat_bucket (
                granularity  INTEGER NOT NULL,
                bucket       INTEGER NOT NULL,
                host         TEXT    NOT NULL,
                requests     INTEGER NOT NULL DEFAULT 0,
                page_views   INTEGER NOT NULL DEFAULT 0,
                blocked      INTEGER NOT NULL DEFAULT 0,
                blocked_403  INTEGER NOT NULL DEFAULT 0,
                blocked_429  INTEGER NOT NULL DEFAULT 0,
                blocked_bot  INTEGER NOT NULL DEFAULT 0,
                status_2xx   INTEGER NOT NULL DEFAULT 0,
                status_3xx   INTEGER NOT NULL DEFAULT 0,
                status_4xx   INTEGER NOT NULL DEFAULT 0,
                status_5xx   INTEGER NOT NULL DEFAULT 0,
                elapsed_sum  INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (granularity, bucket, host)
            ) WITHOUT ROWID;

            CREATE TABLE IF NOT EXISTS stat_dim (
                bucket     INTEGER NOT NULL,
                host       TEXT    NOT NULL,
                dim_type   INTEGER NOT NULL,
                dim_key    TEXT    NOT NULL,
                cnt        INTEGER NOT NULL DEFAULT 0,
                blocked    INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (bucket, host, dim_type, dim_key)
            ) WITHOUT ROWID;

            CREATE TABLE IF NOT EXISTS stat_unique_daily (
                day      INTEGER NOT NULL,
                host     TEXT    NOT NULL,
                kind     INTEGER NOT NULL,
                hash     INTEGER NOT NULL,
                PRIMARY KEY (day, host, kind, hash)
            ) WITHOUT ROWID;

            CREATE TABLE IF NOT EXISTS stat_meta (
                key   TEXT PRIMARY KEY,
                value TEXT
            ) WITHOUT ROWID;

            CREATE TABLE IF NOT EXISTS jev_threat_state (
                ip             TEXT PRIMARY KEY,
                last_request_id INTEGER NOT NULL DEFAULT 0,
                last_evaluated_at INTEGER,
                last_risk      REAL,
                last_batch_id  TEXT,
                last_blocked   INTEGER NOT NULL DEFAULT 0
            ) WITHOUT ROWID;

            CREATE TABLE IF NOT EXISTS jev_threat_batch (
                id             TEXT PRIMARY KEY,
                trigger_type   TEXT NOT NULL,
                started_at     INTEGER NOT NULL,
                finished_at    INTEGER,
                threshold      REAL NOT NULL,
                total_candidates INTEGER NOT NULL DEFAULT 0,
                evaluated_ips  INTEGER NOT NULL DEFAULT 0,
                blocked_ips    INTEGER NOT NULL DEFAULT 0,
                failed_ips     INTEGER NOT NULL DEFAULT 0,
                blacklist_name TEXT,
                status         TEXT NOT NULL,
                error          TEXT
            ) WITHOUT ROWID;

            CREATE TABLE IF NOT EXISTS jev_threat_evaluation (
                id             INTEGER PRIMARY KEY AUTOINCREMENT,
                batch_id       TEXT NOT NULL,
                ip             TEXT NOT NULL,
                evaluated_at   INTEGER NOT NULL,
                first_request_id INTEGER NOT NULL,
                last_request_id INTEGER NOT NULL,
                request_count  INTEGER NOT NULL,
                malicious_probability REAL,
                automated_probability REAL,
                blocked        INTEGER NOT NULL DEFAULT 0,
                model          TEXT NOT NULL,
                latency_ms     INTEGER NOT NULL,
                input_tokens   INTEGER,
                error          TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_jev_threat_eval_batch ON jev_threat_evaluation (batch_id);
            CREATE INDEX IF NOT EXISTS idx_jev_threat_eval_ip ON jev_threat_evaluation (ip, evaluated_at);
            """);

        var version = ExecuteScalarLong(connection, "PRAGMA user_version;");
        if (version < 2 && ExecuteScalarLong(connection,
                "SELECT COUNT(*) FROM pragma_table_info('stat_bucket') WHERE name = 'blocked_bot';") == 0)
        {
            Execute(connection, "ALTER TABLE stat_bucket ADD COLUMN blocked_bot INTEGER NOT NULL DEFAULT 0;");
        }

        if (version < SchemaVersion) Execute(connection, $"PRAGMA user_version={SchemaVersion};");
    }
}

/// <summary>
///     维度类型（stat_dim.dim_type）
/// </summary>
public static class StatDimType
{
    public const int Country = 1;
    public const int Province = 2;
    public const int Os = 3;
    public const int Browser = 4;
    public const int Status = 5;
    public const int RefererHost = 6;
    public const int RefererUrl = 7;
    public const int Path = 8;
}

/// <summary>
///     每日唯一集合类型（stat_unique_daily.kind）
/// </summary>
public static class StatUniqueKind
{
    public const int Ip = 1;
    public const int Visitor = 2;
    public const int BlockedIp = 3;
}
