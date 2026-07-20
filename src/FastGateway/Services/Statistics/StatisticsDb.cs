using Dapper;
using Microsoft.Data.Sqlite;

namespace FastGateway.Services.Statistics;

/// <summary>
///     统计库（data/stats.db）：建库、PRAGMA 与连接工厂。写连接全局唯一（由后台服务持有），
///     读侧使用短生命周期只读池化连接，WAL 下读写互不阻塞。
/// </summary>
public static class StatisticsDb
{
    private const int SchemaVersion = 1;

    private static readonly string DbPath = Path.Combine(AppContext.BaseDirectory, "data", "stats.db");

    private static readonly string WriteConnectionString =
        new SqliteConnectionStringBuilder { DataSource = DbPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();

    private static readonly string ReadConnectionString =
        new SqliteConnectionStringBuilder
            { DataSource = DbPath, Mode = SqliteOpenMode.ReadOnly, Cache = SqliteCacheMode.Private }.ToString();

    private static readonly Lock InitLock = new();
    private static volatile bool _initialized;

    public static bool IsAvailable { get; private set; }

    public static void Initialize(ILogger? logger = null)
    {
        if (_initialized) return;

        lock (InitLock)
        {
            if (_initialized) return;

            try
            {
                var directory = Path.GetDirectoryName(DbPath);
                if (directory != null && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

                using var connection = new SqliteConnection(WriteConnectionString);
                connection.Open();
                connection.Execute("PRAGMA journal_mode=WAL;");
                connection.Execute("PRAGMA synchronous=NORMAL;");
                connection.Execute("PRAGMA auto_vacuum=INCREMENTAL;");
                CreateSchema(connection);
                IsAvailable = true;
            }
            catch (Exception ex)
            {
                // 统计库不可用只降级统计功能，绝不影响代理转发
                IsAvailable = false;
                logger?.LogError(ex, "统计数据库初始化失败，统计功能已降级");
            }
            finally
            {
                _initialized = true;
            }
        }
    }

    public static SqliteConnection OpenWriteConnection()
    {
        var connection = new SqliteConnection(WriteConnectionString);
        connection.Open();
        connection.Execute("PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA busy_timeout=5000; PRAGMA temp_store=MEMORY;");
        return connection;
    }

    public static SqliteConnection OpenReadConnection()
    {
        var connection = new SqliteConnection(ReadConnectionString);
        connection.Open();
        connection.Execute("PRAGMA busy_timeout=3000;");
        return connection;
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        connection.Execute(
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

            CREATE TABLE IF NOT EXISTS stat_bucket (
                granularity  INTEGER NOT NULL,
                bucket       INTEGER NOT NULL,
                host         TEXT    NOT NULL,
                requests     INTEGER NOT NULL DEFAULT 0,
                page_views   INTEGER NOT NULL DEFAULT 0,
                blocked      INTEGER NOT NULL DEFAULT 0,
                blocked_403  INTEGER NOT NULL DEFAULT 0,
                blocked_429  INTEGER NOT NULL DEFAULT 0,
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
            """);

        var version = connection.ExecuteScalar<long>("PRAGMA user_version;");
        if (version < SchemaVersion) connection.Execute($"PRAGMA user_version={SchemaVersion};");
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
