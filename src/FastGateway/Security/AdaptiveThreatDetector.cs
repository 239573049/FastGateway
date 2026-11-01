using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;

namespace FastGateway.Security;

public sealed record AdaptiveRequestProfile(
    string Ip,
    string Path,
    string Method,
    bool ShouldTrack,
    int SignatureWeight,
    bool BlockNow,
    string Reason,
    int RetryAfterSeconds);

public readonly record struct AdaptivePathSignature(int Weight, string Reason);

/// <summary>
///     自适应 HTTP 威胁识别：高危路径立即阻断，普通请求按 4xx、路径多样性、
///     请求突发和异常方法累计评分。短期隔离不会写入持久黑名单。
/// </summary>
public static class AdaptiveThreatDetector
{
    private const int ImmediateBlockThreshold = 70;
    private const int QuarantineScoreThreshold = 60;
    private const int MaxObservationsPerIp = 512;
    private const int MaxStates = 20_000;
    private static readonly TimeSpan ObservationWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StateTtl = TimeSpan.FromHours(2);
    private static readonly TimeSpan StateCleanupInterval = TimeSpan.FromMinutes(2);

    private static readonly ConcurrentDictionary<string, IpState> States =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> LocalAddresses = LoadLocalAddresses();

    private static readonly HashSet<string> RiskyMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "TRACE", "CONNECT", "PROPFIND", "PROPPATCH", "MKCOL", "COPY", "MOVE", "LOCK", "UNLOCK", "DEBUG"
    };

    private static readonly HashSet<string> KnownMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "TRACE", "CONNECT",
        "PROPFIND", "PROPPATCH", "MKCOL", "COPY", "MOVE", "LOCK", "UNLOCK", "DEBUG"
    };

    private static readonly string[] ScannerUserAgents =
    [
        "sqlmap", "nikto", "nmap", "masscan", "zgrab", "acunetix", "nessus", "dirbuster",
        "gobuster", "feroxbuster", "nuclei", "python-requests", "go-http-client"
    ];

    private static int _recordCounter;
    private static long _lastCleanupTicks;

    public static AdaptiveRequestProfile InspectRequest(
        string ip,
        string path,
        string method,
        string? userAgent)
    {
        if (!ShouldTrackIp(ip) || IsIgnoredPath(path))
            return new AdaptiveRequestProfile(ip, path, method, false, 0, false, string.Empty, 0);

        var blocked = AdaptiveThreatStore.GetActive(ip);
        if (blocked != null)
        {
            var retryAfter = Math.Max(1, (int)Math.Ceiling((blocked.ExpiresAt - DateTimeOffset.UtcNow).TotalSeconds));
            return new AdaptiveRequestProfile(ip, path, method, true, blocked.Score, true,
                blocked.Reason, retryAfter);
        }

        var normalized = NormalizePath(path);
        var signature = AnalyzePath(normalized, method, userAgent);
        var weight = signature.Weight;
        var reason = signature.Reason;
        var blockNow = weight >= ImmediateBlockThreshold;
        var retry = blockNow ? 30 * 60 : 0;
        if (blockNow)
        {
            var now = DateTimeOffset.UtcNow;
            AdaptiveThreatStore.Quarantine(new AdaptiveThreatSnapshot
            {
                Ip = ip,
                Score = Math.Max(weight, ImmediateBlockThreshold),
                Reason = reason,
                TotalRequests = 1,
                NotFoundRequests = 0,
                FourXxRequests = 0,
                BurstRequests = 1,
                PathDiversity = 1,
                FirstSeen = now,
                LastSeen = now,
                QuarantinedAt = now,
                ExpiresAt = now.AddMinutes(30),
                LastPath = normalized,
                LastMethod = method.ToUpperInvariant(),
                LastStatus = StatusCodes.Status403Forbidden,
                ServerId = "adaptive"
            });
        }

        return new AdaptiveRequestProfile(ip, normalized, method.ToUpperInvariant(), true,
            weight, blockNow, reason, retry);
    }

    public static AdaptiveThreatSnapshot? ObserveResponse(
        AdaptiveRequestProfile profile,
        int statusCode,
        string? serverId)
    {
        if (!profile.ShouldTrack || IsIgnoredPath(profile.Path)) return null;

        var now = DateTimeOffset.UtcNow;
        var state = States.GetOrAdd(profile.Ip, static _ => new IpState());
        int score;
        int total;
        int notFound;
        int fourXx;
        int burst;
        int diversity;
        DateTimeOffset firstSeen;
        string reason;

        lock (state.Sync)
        {
            state.Prune(now - ObservationWindow);
            state.Observations.Add(new Observation(now, profile.Path, profile.Method,
                Math.Clamp(statusCode, 100, 599), profile.SignatureWeight));
            if (state.Observations.Count > MaxObservationsPerIp)
                state.Observations.RemoveRange(0, state.Observations.Count - MaxObservationsPerIp);

            total = state.Observations.Count;
            notFound = state.Observations.Count(x => x.Status == StatusCodes.Status404NotFound);
            fourXx = state.Observations.Count(x => x.Status is >= 400 and < 500);
            burst = GetMaxBurst(state.Observations, now);
            diversity = state.Observations.Select(x => x.Path).Distinct(StringComparer.Ordinal).Count();
            var signatureScore = Math.Min(60, state.Observations.Sum(x => x.SignatureWeight));
            var fourXxRatio = total == 0 ? 0 : (double)fourXx / total;

            score = signatureScore + Math.Min(25, notFound);
            if (total >= 10 && fourXxRatio >= 0.7) score += 15;
            if (burst >= 30) score += 20;
            else if (burst >= 15) score += 10;
            if (diversity >= 20) score += 15;
            else if (diversity >= 8) score += 8;

            firstSeen = state.Observations[0].Time;
            var reasons = new List<string>(4);
            if (signatureScore > 0) reasons.Add($"敏感请求评分 {signatureScore}");
            if (notFound > 0) reasons.Add($"404 {notFound} 次");
            if (fourXxRatio >= 0.7 && fourXx >= 10) reasons.Add($"4xx 比例 {fourXxRatio:P0}");
            if (burst >= 15) reasons.Add($"60 秒内 {burst} 次请求");
            if (diversity >= 8) reasons.Add($"访问 {diversity} 个不同路径");
            reason = string.Join("；", reasons);
        }

        MaybeCleanup(now);
        if (score < QuarantineScoreThreshold) return null;

        var duration = score >= 90 ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(15);
        return AdaptiveThreatStore.Quarantine(new AdaptiveThreatSnapshot
        {
            Ip = profile.Ip,
            Score = score,
            Reason = reason,
            TotalRequests = total,
            NotFoundRequests = notFound,
            FourXxRequests = fourXx,
            BurstRequests = burst,
            PathDiversity = diversity,
            FirstSeen = firstSeen,
            LastSeen = now,
            QuarantinedAt = now,
            ExpiresAt = now.Add(duration),
            LastPath = profile.Path,
            LastMethod = profile.Method,
            LastStatus = statusCode,
            ServerId = serverId ?? string.Empty
        });
    }

    public static bool ShouldTrackIp(string? rawIp)
    {
        if (!IPAddress.TryParse(rawIp, out var address)) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            return false;
        if (LocalAddresses.Contains(address.ToString())) return false;

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return !(bytes[0] == 0 ||
                     bytes[0] == 10 ||
                     bytes[0] == 127 ||
                     bytes[0] >= 224 ||
                     bytes[0] == 169 && bytes[1] == 254 ||
                     bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                     bytes[0] == 192 && bytes[1] == 168 ||
                     bytes[0] == 100 && bytes[1] is >= 64 and <= 127);
        }

        return !(address.IsIPv6Multicast || address.GetAddressBytes()[0] is 0xfc or 0xfd);
    }

    private static HashSet<string> LoadLocalAddresses()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    var address = unicast.Address;
                    if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
                    result.Add(address.ToString());
                }
            }
        }
        catch
        {
            // 读取网卡失败时仍保留回环/私网过滤，不能让检测器影响请求处理。
        }

        return result;
    }

    public static bool IsIgnoredPath(string path)
    {
        return path.StartsWith("/internal/gateway", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/.well-known/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/__fastgateway/", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/robots.txt", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/sitemap.xml", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/health", StringComparison.OrdinalIgnoreCase) ||
               path.Equals("/api/health", StringComparison.OrdinalIgnoreCase);
    }

    public static AdaptivePathSignature AnalyzePath(string path, string method, string? userAgent = null)
    {
        var normalized = NormalizePath(path);
        return Classify(normalized, method, userAgent);
    }

    private static AdaptivePathSignature Classify(string path, string method, string? userAgent)
    {
        var weights = new List<(int Weight, string Reason)>(4);

        if (Contains(path, "/.env"))
            weights.Add((75, "探测 .env 环境变量"));
        if (ContainsAny(path, "/.git", "/.svn", "/.hg", "/.ds_store"))
            weights.Add((75, "探测版本控制或系统元数据"));
        if (ContainsAny(path, "/etc/passwd", "/proc/self", "/id_rsa", "/.ssh/", "/private_key"))
            weights.Add((90, "敏感文件或路径穿越"));
        if (ContainsAny(path, "../", "..\\", "%2e%2e", "%252e%252e"))
            weights.Add((90, "路径穿越"));
        if (ContainsAny(path, "appsettings.json", "gateway.config", "web.config", "credentials", "secret"))
            weights.Add((85, "探测配置文件或凭据"));
        if (ContainsAny(path, "169.254.169.254", "/latest/meta-data", "/computemetadata"))
            weights.Add((90, "探测云元数据"));
        if (ContainsAny(path, "jndi:", "${", "%24%7b"))
            weights.Add((90, "注入载荷特征"));
        if (path.Contains("/sdk/weblanguage", StringComparison.Ordinal))
            weights.Add((85, "设备命令注入探测"));
        if (ContainsAny(path, "/wp-login", "/wp-admin", "/xmlrpc.php", "/phpmyadmin", "/adminer",
                "/admin/config", "/manager/html", "/actuator", "/console", "/jenkins", "/v2/_catalog",
                "/server-status", "/cgi-bin"))
            weights.Add((60, "管理面或组件漏洞扫描"));
        if (HasScriptExtension(path))
            weights.Add((50, "脚本文件扫描"));
        if (ContainsAny(path, "/mcp", "/sse", "/api/mcp"))
            weights.Add((60, "探测暴露的 AI/SSE 服务"));

        if (RiskyMethods.Contains(method))
            weights.Add((50, $"异常 HTTP 方法 {method}"));
        else if (!KnownMethods.Contains(method))
            weights.Add((30, $"非标准 HTTP 方法 {method}"));

        var ua = userAgent?.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(ua) && ScannerUserAgents.Any(ua.Contains))
            weights.Add((25, "已知扫描器 User-Agent"));

        var weight = Math.Min(90, weights.Sum(x => x.Weight));
        return new AdaptivePathSignature(weight,
            string.Join("；", weights.Select(x => x.Reason).Distinct(StringComparer.Ordinal)));
    }

    private static string NormalizePath(string path)
    {
        var current = path;
        for (var i = 0; i < 2; i++)
        {
            try
            {
                var decoded = Uri.UnescapeDataString(current);
                if (decoded == current) break;
                current = decoded;
            }
            catch (UriFormatException)
            {
                break;
            }
        }

        current = current.Replace('\\', '/').ToLowerInvariant();
        while (current.Contains("//", StringComparison.Ordinal))
            current = current.Replace("//", "/", StringComparison.Ordinal);
        return current.Length > 2048 ? current[..2048] : current;
    }

    private static bool Contains(string path, string value) => path.Contains(value, StringComparison.Ordinal);

    private static bool ContainsAny(string path, params string[] values)
    {
        foreach (var value in values)
        {
            if (path.Contains(value, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private static bool HasScriptExtension(string path)
    {
        return path.EndsWith(".php", StringComparison.Ordinal) ||
               path.EndsWith(".jsp", StringComparison.Ordinal) ||
               path.EndsWith(".aspx", StringComparison.Ordinal) ||
               path.EndsWith(".asp", StringComparison.Ordinal) ||
               path.EndsWith(".cgi", StringComparison.Ordinal);
    }

    private static int GetMaxBurst(List<Observation> observations, DateTimeOffset now)
    {
        var threshold = now - TimeSpan.FromSeconds(60);
        var recent = observations.Where(x => x.Time >= threshold).OrderBy(x => x.Time).ToList();
        var left = 0;
        var maximum = 0;
        for (var right = 0; right < recent.Count; right++)
        {
            while (recent[right].Time - recent[left].Time >= TimeSpan.FromSeconds(60)) left++;
            maximum = Math.Max(maximum, right - left + 1);
        }

        return maximum;
    }

    private static void MaybeCleanup(DateTimeOffset now)
    {
        if ((Interlocked.Increment(ref _recordCounter) & 1023) != 0) return;
        var lastCleanup = Interlocked.Read(ref _lastCleanupTicks);
        if (now.UtcTicks - lastCleanup < StateCleanupInterval.Ticks) return;
        if (Interlocked.CompareExchange(ref _lastCleanupTicks, now.UtcTicks, lastCleanup) != lastCleanup) return;

        foreach (var (ip, state) in States)
        {
            var remove = false;
            lock (state.Sync)
            {
                state.Prune(now - ObservationWindow);
                remove = state.Observations.Count == 0;
            }

            if (remove) States.TryRemove(ip, out _);
        }

        if (States.Count <= MaxStates) return;
        foreach (var ip in States.OrderBy(x => x.Value.LastSeen).Select(x => x.Key).Take(States.Count - MaxStates))
            States.TryRemove(ip, out _);
    }

    private sealed class IpState
    {
        public object Sync { get; } = new();
        public List<Observation> Observations { get; } = new(MaxObservationsPerIp);
        public DateTimeOffset LastSeen => Observations.Count == 0 ? DateTimeOffset.MinValue : Observations[^1].Time;

        public void Prune(DateTimeOffset cutoff)
        {
            var remove = 0;
            while (remove < Observations.Count && Observations[remove].Time < cutoff) remove++;
            if (remove > 0) Observations.RemoveRange(0, remove);
        }
    }

    private sealed record Observation(DateTimeOffset Time, string Path, string Method, int Status, int SignatureWeight);
}
