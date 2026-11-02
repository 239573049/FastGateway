using System.Collections.Concurrent;

namespace FastGateway.Security;

public sealed class AdaptiveThreatSnapshot
{
    public string Ip { get; init; } = string.Empty;
    public int Score { get; init; }
    public string Reason { get; init; } = string.Empty;
    public int TotalRequests { get; init; }
    public int NotFoundRequests { get; init; }
    public int FourXxRequests { get; init; }
    public int BurstRequests { get; init; }
    public int PathDiversity { get; init; }
    public DateTimeOffset FirstSeen { get; init; }
    public DateTimeOffset LastSeen { get; init; }
    public DateTimeOffset QuarantinedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public string LastPath { get; init; } = string.Empty;
    public string LastMethod { get; init; } = string.Empty;
    public int LastStatus { get; init; }
    public string ServerId { get; init; } = string.Empty;
}

/// <summary>
///     自适应威胁产生的短期隔离名单。与持久黑名单分离，避免一次误判形成永久封禁。
/// </summary>
public static class AdaptiveThreatStore
{
    private const int MaxEntries = 10_000;
    private static readonly ConcurrentDictionary<string, AdaptiveThreatSnapshot> Blocked =
        new(StringComparer.OrdinalIgnoreCase);

    public static AdaptiveThreatSnapshot? GetActive(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        if (!Blocked.TryGetValue(ip, out var snapshot)) return null;
        if (snapshot.ExpiresAt > DateTimeOffset.UtcNow) return snapshot;

        Blocked.TryRemove(ip, out _);
        return null;
    }

    public static bool IsBlocked(string? ip) => GetActive(ip) != null;

    public static AdaptiveThreatSnapshot? Quarantine(AdaptiveThreatSnapshot snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Ip) || snapshot.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;

        if (Blocked.TryGetValue(snapshot.Ip, out var existing) && existing.ExpiresAt > snapshot.ExpiresAt)
            return null;

        if (Blocked.Count >= MaxEntries && !Blocked.ContainsKey(snapshot.Ip))
        {
            var oldest = Blocked
                .Where(x => x.Value.ExpiresAt <= DateTimeOffset.UtcNow)
                .OrderBy(x => x.Value.ExpiresAt)
                .Select(x => x.Key)
                .Take(Blocked.Count - MaxEntries + 256)
                .ToArray();
            foreach (var ip in oldest) Blocked.TryRemove(ip, out _);
            if (Blocked.Count >= MaxEntries) return null;
        }

        Blocked[snapshot.Ip] = snapshot;
        return snapshot;
    }

    public static bool Release(string? ip)
    {
        return !string.IsNullOrWhiteSpace(ip) && Blocked.TryRemove(ip, out _);
    }

    public static IReadOnlyList<AdaptiveThreatSnapshot> GetActiveSnapshots()
    {
        var now = DateTimeOffset.UtcNow;
        var expired = new List<string>();
        var result = new List<AdaptiveThreatSnapshot>();

        foreach (var (ip, snapshot) in Blocked)
        {
            if (snapshot.ExpiresAt <= now)
            {
                expired.Add(ip);
                continue;
            }

            result.Add(snapshot);
        }

        foreach (var ip in expired) Blocked.TryRemove(ip, out _);

        return result
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.LastSeen)
            .ToList();
    }
}
