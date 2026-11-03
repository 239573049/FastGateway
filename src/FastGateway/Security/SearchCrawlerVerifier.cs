using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace FastGateway.Security;

public enum CrawlerVerification
{
    /// <summary>未校验（超时、DNS 不可用或超出单轮校验上限）。</summary>
    Unknown,

    /// <summary>反向 DNS 与正向解析均匹配官方域名。</summary>
    Verified,

    /// <summary>自称爬虫但反向 DNS 不匹配：仿冒信号。</summary>
    Failed
}

/// <summary>
///     搜索引擎爬虫校验：PTR 记录必须落在官方域名下，且该主机名正向解析回同一 IP（FCrDNS）。
///     只收录官方文档明确支持反向 DNS 校验的爬虫；googleusercontent.com 等云主机域名不可信，故不收录。
/// </summary>
public static class SearchCrawlerVerifier
{
    private static readonly (string NameFragment, string[] Suffixes)[] Crawlers =
    [
        ("google", [".googlebot.com", ".google.com"]),
        ("bingbot", [".search.msn.com"]),
        ("msnbot", [".search.msn.com"]),
        ("bing preview", [".search.msn.com"]),
        ("bingpreview", [".search.msn.com"]),
        ("baiduspider", [".baidu.com", ".baidu.jp"]),
        ("yandex", [".yandex.ru", ".yandex.net", ".yandex.com"]),
        ("applebot", [".applebot.apple.com"]),
        ("slurp", [".crawl.yahoo.net"]),
        ("petalbot", [".petalsearch.com"])
    ];

    private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan VerifiedTtl = TimeSpan.FromHours(24);
    private static readonly TimeSpan FailedTtl = TimeSpan.FromHours(6);
    private static readonly ConcurrentDictionary<string, (CrawlerVerification Result, DateTimeOffset ExpiresAt)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>根据 UA 解析出的浏览器名判断是否自称可校验的搜索引擎爬虫，返回规范化名称。</summary>
    public static string? GetClaimedCrawler(string? browser)
    {
        if (string.IsNullOrWhiteSpace(browser)) return null;
        foreach (var (fragment, _) in Crawlers)
            if (browser.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return browser;
        return null;
    }

    public static async Task<CrawlerVerification> VerifyAsync(string ip, string crawler,
        CancellationToken cancellationToken)
    {
        if (!IPAddress.TryParse(ip, out var address)) return CrawlerVerification.Unknown;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        var suffixes = Crawlers.FirstOrDefault(x => crawler.Contains(x.NameFragment, StringComparison.OrdinalIgnoreCase))
            .Suffixes;
        if (suffixes == null) return CrawlerVerification.Unknown;

        var cacheKey = $"{address}|{crawler}";
        if (Cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.Result;

        var result = await LookupAsync(address, suffixes, cancellationToken);
        if (result != CrawlerVerification.Unknown)
        {
            if (Cache.Count > 50_000) Cache.Clear();
            Cache[cacheKey] = (result,
                DateTimeOffset.UtcNow + (result == CrawlerVerification.Verified ? VerifiedTtl : FailedTtl));
        }

        return result;
    }

    private static async Task<CrawlerVerification> LookupAsync(IPAddress address, string[] suffixes,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(LookupTimeout);
        try
        {
            var entry = await Dns.GetHostEntryAsync(address.ToString(), timeout.Token);
            var host = entry.HostName.TrimEnd('.');
            if (!suffixes.Any(x => host.EndsWith(x, StringComparison.OrdinalIgnoreCase)))
                return CrawlerVerification.Failed;

            var forward = await Dns.GetHostAddressesAsync(host, timeout.Token);
            return forward.Any(x => (x.IsIPv4MappedToIPv6 ? x.MapToIPv4() : x).Equals(address))
                ? CrawlerVerification.Verified
                : CrawlerVerification.Failed;
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.HostNotFound or SocketError.NoData)
        {
            // 没有 PTR 记录：官方爬虫一定有，视为仿冒
            return CrawlerVerification.Failed;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CrawlerVerification.Unknown;
        }
        catch (SocketException)
        {
            return CrawlerVerification.Unknown;
        }
    }
}
