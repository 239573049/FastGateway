using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace FastGateway.Risk;

public readonly record struct AdminLoginFailure(int RecentFailures, TimeSpan Delay, TimeSpan? LockedFor);

public readonly record struct AdminLoginLock(
    string Subject,
    bool IsSubnet,
    DateTimeOffset LockedAt,
    DateTimeOffset LockedUntil);

/// <summary>
///     管理后台登录失败限制：按来源 IP 与所在子网统计窗口内的失败次数，超过阈值后渐进延迟并临时锁定。
///     只锁定来源、不做全局锁定，否则攻击者可以借此把管理员挡在门外。
/// </summary>
public sealed class AdminLoginGuard
{
    /// <summary>管理后台识别客户端 IP 的来源（与 <see cref="Core.Entities.ClientIpSource" /> 对应）</summary>
    public const string ClientIpSourceSettingKey = "admin_client_ip_source";

    public static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    // 锁定时长必须长于统计窗口：解锁时旧失败记录已全部滑出窗口，不会解锁后立即再次锁定
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    public const int DelayThreshold = LoginFailureDelay.Threshold;
    public const int IpLockThreshold = 20;
    public const int SubnetLockThreshold = 60;

    // 大规模分布式爆破时字典会随来源数增长，满员后先淘汰空闲条目，仍满则不再追踪新来源
    private const int MaxEntries = 50_000;
    private const int CleanupEveryRecords = 1024; // 必须是 2 的幂

    private const string UnknownIp = "unknown";

    private readonly ConcurrentDictionary<string, FailureEntry> _ips = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, FailureEntry> _subnets = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AdminLoginGuard> _logger;
    private int _recordCounter;

    public AdminLoginGuard(TimeProvider timeProvider, ILogger<AdminLoginGuard> logger)
    {
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    ///     来源 IP 或其子网仍处于锁定期时返回剩余时长，否则返回 null
    /// </summary>
    public TimeSpan? GetLockRemaining(string? ip)
    {
        var now = _timeProvider.GetUtcNow().UtcTicks;
        var key = NormalizeKey(ip);
        var remaining = GetLockRemaining(_ips, key, now);

        var subnet = GetSubnetKey(key);
        if (subnet == null) return remaining;

        var subnetRemaining = GetLockRemaining(_subnets, subnet, now);
        if (remaining == null) return subnetRemaining;
        if (subnetRemaining == null) return remaining;
        return subnetRemaining > remaining ? subnetRemaining : remaining;
    }

    /// <summary>
    ///     该 IP 窗口内的失败次数
    /// </summary>
    public int GetRecentFailures(string? ip)
    {
        return _ips.TryGetValue(NormalizeKey(ip), out var entry)
            ? entry.Count(_timeProvider.GetUtcNow().UtcTicks, Window.Ticks)
            : 0;
    }

    public AdminLoginFailure RecordFailure(string? ip)
    {
        var now = _timeProvider.GetUtcNow().UtcTicks;
        if ((Interlocked.Increment(ref _recordCounter) & (CleanupEveryRecords - 1)) == 0) Cleanup(now);

        var key = NormalizeKey(ip);
        var ipRecord = Record(_ips, key, now, IpLockThreshold);
        if (ipRecord.LockedNow)
            _logger.LogWarning("管理后台登录：{Ip} 在 {Window} 分钟内失败 {Count} 次，锁定 {Lock} 分钟",
                key, Window.TotalMinutes, ipRecord.Count, LockDuration.TotalMinutes);

        var subnet = GetSubnetKey(key);
        var subnetLocked = false;
        if (subnet != null)
        {
            var subnetRecord = Record(_subnets, subnet, now, SubnetLockThreshold);
            subnetLocked = subnetRecord.LockedNow;
            if (subnetLocked)
                _logger.LogWarning("管理后台登录：子网 {Subnet} 在 {Window} 分钟内失败 {Count} 次，锁定 {Lock} 分钟",
                    subnet, Window.TotalMinutes, subnetRecord.Count, LockDuration.TotalMinutes);
        }

        TimeSpan? lockedFor = ipRecord.LockedNow || subnetLocked ? LockDuration : null;
        return new AdminLoginFailure(ipRecord.Count, GetDelay(ipRecord.Count), lockedFor);
    }

    /// <summary>
    ///     登录成功后清除该 IP 的失败记录；子网记录保留，避免攻击者在同一子网内用一个可登录的来源洗掉计数
    /// </summary>
    public void RecordSuccess(string? ip)
    {
        _ips.TryRemove(NormalizeKey(ip), out _);
    }

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return LoginFailureDelay.DelayAsync(delay, _timeProvider, cancellationToken);
    }

    public static TimeSpan GetDelay(int recentFailures)
    {
        return LoginFailureDelay.For(recentFailures);
    }

    /// <summary>
    ///     当前处于锁定期的 IP 与子网，供风控事件页展示
    /// </summary>
    public IReadOnlyList<AdminLoginLock> GetActiveLocks()
    {
        var now = _timeProvider.GetUtcNow().UtcTicks;
        var result = new List<AdminLoginLock>();
        Collect(_ips, isSubnet: false);
        Collect(_subnets, isSubnet: true);
        return result;

        void Collect(ConcurrentDictionary<string, FailureEntry> entries, bool isSubnet)
        {
            foreach (var (key, entry) in entries)
            {
                var lockedUntil = entry.LockedUntil;
                if (lockedUntil > now)
                    result.Add(new AdminLoginLock(key, isSubnet,
                        new DateTimeOffset(lockedUntil - LockDuration.Ticks, TimeSpan.Zero),
                        new DateTimeOffset(lockedUntil, TimeSpan.Zero)));
            }
        }
    }

    /// <summary>
    ///     手动解除锁定并清空失败记录
    /// </summary>
    public bool Unlock(string subject, bool isSubnet)
    {
        return (isSubnet ? _subnets : _ips).TryRemove(subject, out _);
    }

    /// <summary>
    ///     先各自哈希再比较：定长输入使比较耗时与密码长度、匹配前缀都无关
    /// </summary>
    public static bool PasswordMatches(string? candidate, string expected)
    {
        var candidateHash = SHA256.HashData(Encoding.UTF8.GetBytes(candidate ?? string.Empty));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(candidateHash, expectedHash);
    }

    public static string? GetSubnetKey(string? ip)
    {
        return IpSubnet.GetKey(ip);
    }

    private static string NormalizeKey(string? ip)
    {
        return string.IsNullOrWhiteSpace(ip) ? UnknownIp : ip;
    }

    private static TimeSpan? GetLockRemaining(ConcurrentDictionary<string, FailureEntry> entries, string key,
        long now)
    {
        if (!entries.TryGetValue(key, out var entry)) return null;

        var lockedUntil = entry.LockedUntil;
        return lockedUntil > now ? TimeSpan.FromTicks(lockedUntil - now) : null;
    }

    private (int Count, bool LockedNow) Record(ConcurrentDictionary<string, FailureEntry> entries, string key,
        long now, int threshold)
    {
        if (!entries.TryGetValue(key, out var entry))
        {
            if (entries.Count >= MaxEntries)
            {
                Cleanup(now);
                if (entries.Count >= MaxEntries) return (0, false);
            }

            entry = entries.GetOrAdd(key, _ => new FailureEntry(threshold));
        }

        return entry.Record(now, Window.Ticks, LockDuration.Ticks);
    }

    private void Cleanup(long now)
    {
        Evict(_ips, now);
        Evict(_subnets, now);
    }

    private static void Evict(ConcurrentDictionary<string, FailureEntry> entries, long now)
    {
        foreach (var (key, entry) in entries)
        {
            if (entry.IsIdle(now, Window.Ticks)) entries.TryRemove(key, out _);
        }
    }

    private sealed class FailureEntry(int threshold)
    {
        private readonly Lock _sync = new();

        // 只需判断窗口内是否达到阈值，因此最多保留最近 threshold 次失败的时间
        private readonly long[] _failures = new long[threshold];
        private int _next;
        private long _lockedUntil;
        private long _lastFailure;

        public long LockedUntil => Volatile.Read(ref _lockedUntil);

        public (int Count, bool LockedNow) Record(long now, long windowTicks, long lockTicks)
        {
            lock (_sync)
            {
                _failures[_next] = now;
                _next = (_next + 1) % _failures.Length;
                _lastFailure = now;

                var count = 0;
                foreach (var failure in _failures)
                {
                    if (failure != 0 && now - failure < windowTicks) count++;
                }

                if (count < _failures.Length || _lockedUntil > now) return (count, false);

                Volatile.Write(ref _lockedUntil, now + lockTicks);
                return (count, true);
            }
        }

        public int Count(long now, long windowTicks)
        {
            lock (_sync)
            {
                return _failures.Count(failure => failure != 0 && now - failure < windowTicks);
            }
        }

        public bool IsIdle(long now, long windowTicks)
        {
            lock (_sync)
            {
                return now - _lastFailure >= windowTicks && _lockedUntil <= now;
            }
        }
    }
}
