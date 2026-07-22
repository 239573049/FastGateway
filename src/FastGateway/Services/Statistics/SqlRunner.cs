using Microsoft.Data.Sqlite;

namespace FastGateway.Services.Statistics;

/// <summary>
///     轻量 ADO.NET 帮助器，替代 Dapper（Dapper 的对象映射依赖运行时 IL emit，与 Native AOT 不兼容）。
///     所有参数显式绑定，所有结果集通过手写 mapper 委托映射，全程无反射。
/// </summary>
internal static class SqlRunner
{
    /// <summary>SQL 命名参数（名称含 @ 前缀，值可空）。</summary>
    public readonly record struct P(string Name, object? Value);

    private static SqliteCommand CreateCommand(
        SqliteConnection connection, string sql, SqliteTransaction? transaction, ReadOnlySpan<P> parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        if (transaction != null) command.Transaction = transaction;
        foreach (var p in parameters)
            command.Parameters.AddWithValue(p.Name, p.Value ?? DBNull.Value);
        return command;
    }

    public static int Execute(
        SqliteConnection connection, string sql, SqliteTransaction? transaction = null, params P[] parameters)
    {
        using var command = CreateCommand(connection, sql, transaction, parameters);
        return command.ExecuteNonQuery();
    }

    public static long ExecuteScalarLong(
        SqliteConnection connection, string sql, SqliteTransaction? transaction = null, params P[] parameters)
    {
        using var command = CreateCommand(connection, sql, transaction, parameters);
        var value = command.ExecuteScalar();
        return value is null or DBNull ? 0L : Convert.ToInt64(value);
    }

    public static int ExecuteScalarInt(
        SqliteConnection connection, string sql, SqliteTransaction? transaction = null, params P[] parameters)
    {
        using var command = CreateCommand(connection, sql, transaction, parameters);
        var value = command.ExecuteScalar();
        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    public static string? ExecuteScalarString(
        SqliteConnection connection, string sql, SqliteTransaction? transaction = null, params P[] parameters)
    {
        using var command = CreateCommand(connection, sql, transaction, parameters);
        var value = command.ExecuteScalar();
        return value is null or DBNull ? null : Convert.ToString(value);
    }

    /// <summary>手写 mapper 映射结果集为列表。</summary>
    public static List<T> Query<T>(
        SqliteConnection connection, string sql, Func<SqliteDataReader, T> map,
        SqliteTransaction? transaction = null, params P[] parameters)
    {
        using var command = CreateCommand(connection, sql, transaction, parameters);
        using var reader = command.ExecuteReader();
        var result = new List<T>();
        while (reader.Read()) result.Add(map(reader));
        return result;
    }

    /// <summary>
    ///     批量执行同一条 SQL（替代 Dapper 的 Execute(sql, IEnumerable)）：复用一条命令，
    ///     每个元素通过 bind 委托显式设置参数值后执行一次。
    /// </summary>
    public static void ExecuteBatch<T>(
        SqliteConnection connection, string sql, SqliteTransaction transaction,
        IReadOnlyCollection<T> items, Action<SqliteCommand, T> bind)
    {
        if (items.Count == 0) return;

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;

        foreach (var item in items)
        {
            command.Parameters.Clear();
            bind(command, item);
            command.ExecuteNonQuery();
        }
    }

    // ===== 类型安全的列读取（按序号，处理 NULL）=====
    public static long GetLong(this SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? 0L : r.GetInt64(ordinal);
    public static int GetInt(this SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? 0 : r.GetInt32(ordinal);
    public static double GetDoubleOrZero(this SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? 0d : r.GetDouble(ordinal);
    public static string GetStringOrEmpty(this SqliteDataReader r, int ordinal) => r.IsDBNull(ordinal) ? string.Empty : r.GetString(ordinal);
}
