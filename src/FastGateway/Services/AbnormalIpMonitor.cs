using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace FastGateway.Services;

public sealed class AbnormalIpSnapshot
{
    public string Ip { get; set; } = string.Empty;

    public int WindowErrorCount { get; set; }

    public long TotalErrorCount { get; set; }

    public DateTime FirstSeen { get; set; }

    public DateTime LastSeen { get; set; }

    public string? LastErrorDescription { get; set; }

    public string? TopErrorDescription { get; set; }

    public string? LastPath { get; set; }

    public string? LastMethod { get; set; }

    public int? LastStatusCode { get; set; }

    public string? LastServerId { get; set; }
}

public static class AbnormalIpMonitor
{
    private static readonly TimeSpan AbnormalRetention = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan EntryTtl = TimeSpan.FromHours(24);

    private const int AbnormalThreshold = 20;
    private const int MaxDescriptionsPerIp = 20;
    private const int BucketSeconds = 5;
    private const int BucketCount = 12; // 12 * 5s = 60s window

    private static readonly ConcurrentDictionary<string, IpEntry> Entries = new(StringComparer.OrdinalIgnoreCase);

    public static void Record(
        string ip,
        string description,
        string? path,
        string? method,
        int? statusCode,
        string? serverId)
    {
        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var nowUtcTicks = now.UtcDateTime.Ticks;
        var nowBucketKey = now.ToUnixTimeSeconds() / BucketSeconds;

        var entry = Entries.GetOrAdd(ip, _ => new IpEntry());
        entry.Record(nowUtcTicks, nowBucketKey, description, path, method, statusCode, serverId);
    }

    public static IReadOnlyList<AbnormalIpSnapshot> GetAbnormalIps()
    {
        var now = DateTimeOffset.UtcNow;
        var nowUtcTicks = now.UtcDateTime.Ticks;
        var nowBucketKey = now.ToUnixTimeSeconds() / BucketSeconds;
        var result = new List<AbnormalIpSnapshot>();
        var toRemove = new List<string>();

        foreach (var (ip, entry) in Entries)
        {
            var snapshot = entry.TrySnapshot(ip, nowUtcTicks, nowBucketKey);
            if (snapshot == null)
            {
                if (entry.CanEvict(nowUtcTicks))
                {
                    toRemove.Add(ip);
                }

                continue;
            }

            result.Add(snapshot);
        }

        foreach (var ip in toRemove)
        {
            Entries.TryRemove(ip, out _);
        }

        return result
            .OrderByDescending(x => x.WindowErrorCount)
            .ThenByDescending(x => x.LastSeen)
            .ToList();
    }

    private sealed class IpEntry
    {
        private readonly long[] _bucketKeys = new long[BucketCount];
        private readonly int[] _bucketCounts = new int[BucketCount];
        private readonly ConcurrentDictionary<string, long> _descriptions = new(StringComparer.Ordinal);

        private long _total;
        private long _firstSeenUtcTicks;
        private long _lastSeenUtcTicks;
        private long _lastAbnormalUtcTicks;

        private string? _lastError;
        private string? _lastPath;
        private string? _lastMethod;
        private string? _lastServerId;
        private int _lastStatusCode = -1;

        public void Record(
            long nowUtcTicks,
            long nowBucketKey,
            string description,
            string? path,
            string? method,
            int? statusCode,
            string? serverId)
        {
            Interlocked.Increment(ref _total);
            Interlocked.CompareExchange(ref _firstSeenUtcTicks, nowUtcTicks, 0);
            Interlocked.Exchange(ref _lastSeenUtcTicks, nowUtcTicks);

            Interlocked.Exchange(ref _lastError, description);
            if (path != null) Interlocked.Exchange(ref _lastPath, path);
            if (method != null) Interlocked.Exchange(ref _lastMethod, method);
            if (serverId != null) Interlocked.Exchange(ref _lastServerId, serverId);
            Interlocked.Exchange(ref _lastStatusCode, statusCode ?? -1);

            if (_descriptions.Count < MaxDescriptionsPerIp || _descriptions.ContainsKey(description))
            {
                _descriptions.AddOrUpdate(description, 1, (_, current) => current + 1);
            }

            IncrementBucket(nowBucketKey);

            var windowCount = GetWindowErrorCount(nowBucketKey);
            if (windowCount >= AbnormalThreshold)
            {
                Interlocked.Exchange(ref _lastAbnormalUtcTicks, nowUtcTicks);
            }
        }

        public AbnormalIpSnapshot? TrySnapshot(string ip, long nowUtcTicks, long nowBucketKey)
        {
            var windowCount = GetWindowErrorCount(nowBucketKey);
            var lastAbnormalUtcTicks = Volatile.Read(ref _lastAbnormalUtcTicks);
            var isAbnormal = windowCount >= AbnormalThreshold ||
                             (lastAbnormalUtcTicks != 0 &&
                              nowUtcTicks - lastAbnormalUtcTicks <= AbnormalRetention.Ticks);

            if (!isAbnormal)
            {
                return null;
            }

            var firstSeenUtcTicks = Volatile.Read(ref _firstSeenUtcTicks);
            var lastSeenUtcTicks = Volatile.Read(ref _lastSeenUtcTicks);

            var firstSeen = firstSeenUtcTicks == 0
                ? DateTime.UtcNow
                : new DateTime(firstSeenUtcTicks, DateTimeKind.Utc);

            var lastSeen = lastSeenUtcTicks == 0
                ? DateTime.UtcNow
                : new DateTime(lastSeenUtcTicks, DateTimeKind.Utc);

            var lastStatusCode = Volatile.Read(ref _lastStatusCode);

            return new AbnormalIpSnapshot
            {
                Ip = ip,
                WindowErrorCount = windowCount,
                TotalErrorCount = Volatile.Read(ref _total),
                FirstSeen = firstSeen.ToLocalTime(),
                LastSeen = lastSeen.ToLocalTime(),
                LastErrorDescription = Volatile.Read(ref _lastError),
                TopErrorDescription = GetTopDescription(),
                LastPath = Volatile.Read(ref _lastPath),
                LastMethod = Volatile.Read(ref _lastMethod),
                LastStatusCode = lastStatusCode == -1 ? null : lastStatusCode,
                LastServerId = Volatile.Read(ref _lastServerId)
            };
        }

        public bool CanEvict(long nowUtcTicks)
        {
            var lastSeenUtcTicks = Volatile.Read(ref _lastSeenUtcTicks);
            if (lastSeenUtcTicks == 0) return false;
            return nowUtcTicks - lastSeenUtcTicks >= EntryTtl.Ticks;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IncrementBucket(long bucketKey)
        {
            var index = (int)(bucketKey % BucketCount);
            while (true)
            {
                var existing = Volatile.Read(ref _bucketKeys[index]);
                if (existing == bucketKey) break;

                if (Interlocked.CompareExchange(ref _bucketKeys[index], bucketKey, existing) == existing)
                {
                    Interlocked.Exchange(ref _bucketCounts[index], 0);
                    break;
                }
            }

            Interlocked.Increment(ref _bucketCounts[index]);
        }

        private int GetWindowErrorCount(long nowBucketKey)
        {
            var minKey = nowBucketKey - (BucketCount - 1);
            var sum = 0;
            for (var i = 0; i < BucketCount; i++)
            {
                var key = Volatile.Read(ref _bucketKeys[i]);
                if (key < minKey) continue;

                sum += Volatile.Read(ref _bucketCounts[i]);
            }

            return sum;
        }

        private string? GetTopDescription()
        {
            if (_descriptions.IsEmpty) return null;

            string? best = null;
            long bestCount = 0;
            foreach (var (desc, count) in _descriptions)
            {
                if (count > bestCount)
                {
                    bestCount = count;
                    best = desc;
                }
            }

            return best;
        }
    }
}
