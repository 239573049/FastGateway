using System.Net;
using System.Net.Sockets;
using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;

namespace FastGateway.Services.Statistics;

/// <summary>
///     离线 IP 归属地解析（ip2region.xdb 全量加载内存，μs 级查询）。
///     仅由统计后台消费者单线程调用，缓存用普通字典即可。
/// </summary>
public static class GeoIpService
{
    public const string Unknown = "其他";
    public const string Intranet = "内网";
    public const string China = "中国";

    private const int CacheLimit = 65_536;

    private static readonly Dictionary<string, (string Country, string Province)> Cache = new();
    private static ISearcher? _searcher;
    private static bool _loadFailed;

    public static bool IsReady => _searcher != null;

    public static void Initialize(ILogger? logger = null)
    {
        if (_searcher != null || _loadFailed) return;

        try
        {
            var xdbPath = Path.Combine(AppContext.BaseDirectory, "ip2region.xdb");
            if (!File.Exists(xdbPath))
            {
                _loadFailed = true;
                logger?.LogWarning("未找到 ip2region.xdb，IP 归属地解析已降级为 \"{Unknown}\"", Unknown);
                return;
            }

            _searcher = new Searcher(CachePolicy.Content, xdbPath);
        }
        catch (Exception ex)
        {
            _loadFailed = true;
            logger?.LogError(ex, "ip2region.xdb 加载失败，IP 归属地解析已降级");
        }
    }

    public static (string Country, string Province) Resolve(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return (Unknown, string.Empty);

        if (Cache.TryGetValue(ip, out var cached)) return cached;

        var result = ResolveCore(ip);

        if (Cache.Count >= CacheLimit) Cache.Clear();
        Cache[ip] = result;
        return result;
    }

    private static (string Country, string Province) ResolveCore(string ip)
    {
        if (!IPAddress.TryParse(ip, out var address)) return (Unknown, string.Empty);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
            ip = address.ToString();
        }

        if (IsPrivate(address)) return (Intranet, string.Empty);

        // 标准 ip2region.xdb 仅支持 IPv4
        if (address.AddressFamily != AddressFamily.InterNetwork) return (Unknown, string.Empty);

        var searcher = _searcher;
        if (searcher == null) return (Unknown, string.Empty);

        try
        {
            // ip2region_v4.xdb 区域格式: 国家|省份|城市|ISP|国家代码，缺失字段为 0
            var region = searcher.Search(ip);
            if (string.IsNullOrEmpty(region)) return (Unknown, string.Empty);

            var parts = region.Split('|');
            var country = Normalize(parts.Length > 0 ? parts[0] : null);
            var province = Normalize(parts.Length > 1 ? parts[1] : null);

            if (country == Unknown && province.Length > 0) country = China;
            return (country, country == China ? province : string.Empty);
        }
        catch
        {
            return (Unknown, string.Empty);
        }
    }

    private static string Normalize(string? value)
    {
        return string.IsNullOrEmpty(value) || value == "0" ? Unknown : value;
    }

    private static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address)) return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6UniqueLocal || address.IsIPv6SiteLocal;

        Span<byte> bytes = stackalloc byte[4];
        if (!address.TryWriteBytes(bytes, out _)) return false;

        return bytes[0] switch
        {
            10 => true,
            100 => bytes[1] >= 64 && bytes[1] <= 127,
            169 => bytes[1] == 254,
            172 => bytes[1] >= 16 && bytes[1] <= 31,
            192 => bytes[1] == 168,
            _ => false
        };
    }
}
