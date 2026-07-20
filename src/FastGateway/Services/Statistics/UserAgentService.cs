using MyCSharp.HttpUserAgentParser;

namespace FastGateway.Services.Statistics;

/// <summary>
///     User-Agent 解析（OS + 浏览器）。仅由统计后台消费者单线程调用。
/// </summary>
public static class UserAgentService
{
    public const string Unknown = "未知";

    private const int CacheLimit = 10_000;

    private static readonly Dictionary<string, (string Os, string Browser)> Cache = new();

    public static (string Os, string Browser) Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return (Unknown, Unknown);

        if (Cache.TryGetValue(userAgent, out var cached)) return cached;

        var result = ParseCore(userAgent);

        if (Cache.Count >= CacheLimit) Cache.Clear();
        Cache[userAgent] = result;
        return result;
    }

    private static (string Os, string Browser) ParseCore(string userAgent)
    {
        try
        {
            var info = HttpUserAgentParser.Parse(userAgent);

            var os = info.Platform?.Name;
            var browser = info.Name;

            if (string.IsNullOrEmpty(browser) && info.Type == HttpUserAgentType.Robot) browser = "Bot";
            if (string.IsNullOrEmpty(os) && StartsWithAny(userAgent, "curl/", "Wget/")) os = userAgent.Split('/')[0];

            return (string.IsNullOrEmpty(os) ? Unknown : os,
                string.IsNullOrEmpty(browser) ? Unknown : browser);
        }
        catch
        {
            return (Unknown, Unknown);
        }
    }

    private static bool StartsWithAny(string value, params string[] prefixes)
    {
        foreach (var prefix in prefixes)
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }
}
