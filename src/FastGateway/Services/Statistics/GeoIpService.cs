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

    /// <summary>
    ///     ip2region / GeoJSON 常见英文国名 → 中文展示名。
    /// </summary>
    private static readonly Dictionary<string, string> CountryZhMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Afghanistan"] = "阿富汗",
        ["Albania"] = "阿尔巴尼亚",
        ["Algeria"] = "阿尔及利亚",
        ["Angola"] = "安哥拉",
        ["Argentina"] = "阿根廷",
        ["Armenia"] = "亚美尼亚",
        ["Australia"] = "澳大利亚",
        ["Austria"] = "奥地利",
        ["Azerbaijan"] = "阿塞拜疆",
        ["Bahamas"] = "巴哈马",
        ["Bangladesh"] = "孟加拉国",
        ["Belarus"] = "白俄罗斯",
        ["Belgium"] = "比利时",
        ["Belize"] = "伯利兹",
        ["Benin"] = "贝宁",
        ["Bermuda"] = "百慕大群岛",
        ["Bhutan"] = "不丹",
        ["Bolivia"] = "玻利维亚",
        ["Bosnia and Herz."] = "波斯尼亚和黑塞哥维那",
        ["Botswana"] = "博茨瓦纳",
        ["Brazil"] = "巴西",
        ["Britain"] = "英国",
        ["Brunei"] = "文莱",
        ["Brunei Darussalam"] = "文莱",
        ["Bulgaria"] = "保加利亚",
        ["Burkina Faso"] = "布基纳法索",
        ["Burma"] = "缅甸",
        ["Burundi"] = "布隆迪",
        ["Cambodia"] = "柬埔寨",
        ["Cameroon"] = "喀麦隆",
        ["Canada"] = "加拿大",
        ["Central African Rep."] = "中非共和国",
        ["Chad"] = "乍得",
        ["Chile"] = "智利",
        ["China"] = "中国",
        ["Colombia"] = "哥伦比亚",
        ["Congo"] = "刚果",
        ["Costa Rica"] = "哥斯达黎加",
        ["Croatia"] = "克罗地亚",
        ["Cuba"] = "古巴",
        ["Cyprus"] = "塞浦路斯",
        ["Czech Rep."] = "捷克共和国",
        ["Czech Republic"] = "捷克",
        ["Czechia"] = "捷克",
        ["Côte d'Ivoire"] = "科特迪瓦",
        ["Dem. Rep. Congo"] = "民主刚果",
        ["Dem. Rep. Korea"] = "朝鲜",
        ["Denmark"] = "丹麦",
        ["Djibouti"] = "吉布提",
        ["Dominican Rep."] = "多米尼加共和国",
        ["Ecuador"] = "厄瓜多尔",
        ["Egypt"] = "埃及",
        ["El Salvador"] = "萨尔瓦多",
        ["England"] = "英国",
        ["Eq. Guinea"] = "赤道几内亚",
        ["Eritrea"] = "厄立特里亚",
        ["Estonia"] = "爱沙尼亚",
        ["Ethiopia"] = "埃塞俄比亚",
        ["Falkland Is."] = "福克兰群岛",
        ["Fiji"] = "斐济",
        ["Finland"] = "芬兰",
        ["Fr. S. Antarctic Lands"] = "法属南部领地",
        ["France"] = "法国",
        ["French Guiana"] = "法属圭亚那",
        ["Gabon"] = "加蓬",
        ["Gambia"] = "冈比亚",
        ["Georgia"] = "格鲁吉亚",
        ["Germany"] = "德国",
        ["Ghana"] = "加纳",
        ["Great Britain"] = "英国",
        ["Greece"] = "希腊",
        ["Greenland"] = "格陵兰",
        ["Guatemala"] = "危地马拉",
        ["Guinea"] = "几内亚",
        ["Guinea-Bissau"] = "几内亚比绍",
        ["Guyana"] = "圭亚那",
        ["Haiti"] = "海地",
        ["Heard I. and McDonald Is."] = "赫德岛和麦克唐纳群岛",
        ["Holland"] = "荷兰",
        ["Honduras"] = "洪都拉斯",
        ["Hong Kong"] = "中国香港",
        ["Hungary"] = "匈牙利",
        ["Iceland"] = "冰岛",
        ["India"] = "印度",
        ["Indonesia"] = "印度尼西亚",
        ["Iran"] = "伊朗",
        ["Iran, Islamic Republic of"] = "伊朗",
        ["Iraq"] = "伊拉克",
        ["Ireland"] = "爱尔兰",
        ["Israel"] = "以色列",
        ["Italy"] = "意大利",
        ["Ivory Coast"] = "象牙海岸",
        ["Jamaica"] = "牙买加",
        ["Japan"] = "日本",
        ["Jordan"] = "约旦",
        ["Kashmir"] = "克什米尔",
        ["Kazakhstan"] = "哈萨克斯坦",
        ["Kenya"] = "肯尼亚",
        ["Korea"] = "韩国",
        ["Korea, Democratic People's Republic of"] = "朝鲜",
        ["Korea, Republic of"] = "韩国",
        ["Kosovo"] = "科索沃",
        ["Kuwait"] = "科威特",
        ["Kyrgyzstan"] = "吉尔吉斯斯坦",
        ["Lao PDR"] = "老挝人民民主共和国",
        ["Lao People's Democratic Republic"] = "老挝",
        ["Laos"] = "老挝",
        ["Latvia"] = "拉脱维亚",
        ["Lebanon"] = "黎巴嫩",
        ["Lesotho"] = "莱索托",
        ["Liberia"] = "利比里亚",
        ["Libya"] = "利比亚",
        ["Lithuania"] = "立陶宛",
        ["Luxembourg"] = "卢森堡",
        ["Macao"] = "中国澳门",
        ["Macau"] = "中国澳门",
        ["Macedonia"] = "马其顿",
        ["Madagascar"] = "马达加斯加",
        ["Malawi"] = "马拉维",
        ["Malaysia"] = "马来西亚",
        ["Mali"] = "马里",
        ["Mauritania"] = "毛里塔尼亚",
        ["Mexico"] = "墨西哥",
        ["Moldova"] = "摩尔多瓦",
        ["Mongolia"] = "蒙古",
        ["Montenegro"] = "黑山",
        ["Morocco"] = "摩洛哥",
        ["Mozambique"] = "莫桑比克",
        ["Myanmar"] = "缅甸",
        ["Namibia"] = "纳米比亚",
        ["Nepal"] = "尼泊尔",
        ["Netherlands"] = "荷兰",
        ["New Caledonia"] = "新喀里多尼亚",
        ["New Zealand"] = "新西兰",
        ["Nicaragua"] = "尼加拉瓜",
        ["Niger"] = "尼日尔",
        ["Nigeria"] = "尼日利亚",
        ["North Korea"] = "朝鲜",
        ["Northern Cyprus"] = "北塞浦路斯",
        ["Norway"] = "挪威",
        ["Oman"] = "阿曼",
        ["Pakistan"] = "巴基斯坦",
        ["Panama"] = "巴拿马",
        ["Papua New Guinea"] = "巴布亚新几内亚",
        ["Paraguay"] = "巴拉圭",
        ["Peru"] = "秘鲁",
        ["Philippines"] = "菲律宾",
        ["Poland"] = "波兰",
        ["Portugal"] = "葡萄牙",
        ["Puerto Rico"] = "波多黎各",
        ["Qatar"] = "卡塔尔",
        ["Republic of Korea"] = "韩国",
        ["Republic of Seychelles"] = "塞舌尔共和国",
        ["Republic of the Congo"] = "刚果共和国",
        ["Romania"] = "罗马尼亚",
        ["Russia"] = "俄罗斯",
        ["Russian Federation"] = "俄罗斯",
        ["Rwanda"] = "卢旺达",
        ["S. Geo. and S. Sandw. Is."] = "南乔治亚和南桑德威奇群岛",
        ["S. Sudan"] = "南苏丹",
        ["Samoa"] = "萨摩亚",
        ["Saudi Arabia"] = "沙特阿拉伯",
        ["Senegal"] = "塞内加尔",
        ["Serbia"] = "塞尔维亚",
        ["Sierra Leone"] = "塞拉利昂",
        ["Singapore"] = "新加坡",
        ["Slovakia"] = "斯洛伐克",
        ["Slovenia"] = "斯洛文尼亚",
        ["Solomon Is."] = "所罗门群岛",
        ["Somalia"] = "索马里",
        ["Somaliland"] = "索马里兰",
        ["South Africa"] = "南非",
        ["South Korea"] = "韩国",
        ["Spain"] = "西班牙",
        ["Sri Lanka"] = "斯里兰卡",
        ["Sudan"] = "苏丹",
        ["Suriname"] = "苏里南",
        ["Swaziland"] = "斯威士兰",
        ["Sweden"] = "瑞典",
        ["Switzerland"] = "瑞士",
        ["Syria"] = "叙利亚",
        ["Syrian Arab Republic"] = "叙利亚",
        ["Taiwan"] = "中国台湾",
        ["Tajikistan"] = "塔吉克斯坦",
        ["Tanzania"] = "坦桑尼亚",
        ["Thailand"] = "泰国",
        ["The Kingdom of Tonga"] = "汤加王国",
        ["The Netherlands"] = "荷兰",
        ["Timor-Leste"] = "东帝汶",
        ["Togo"] = "多哥",
        ["Trinidad and Tobago"] = "特立尼达和多巴哥",
        ["Tunisia"] = "突尼斯",
        ["Turkey"] = "土耳其",
        ["Turkmenistan"] = "土库曼斯坦",
        ["U.S."] = "美国",
        ["U.S.A."] = "美国",
        ["UAE"] = "阿联酋",
        ["Uganda"] = "乌干达",
        ["UK"] = "英国",
        ["Ukraine"] = "乌克兰",
        ["United Arab Emirates"] = "阿拉伯联合酋长国",
        ["United Kingdom"] = "英国",
        ["United Republic of Tanzania"] = "坦桑尼亚联合共和国",
        ["United States"] = "美国",
        ["United States of America"] = "美国",
        ["Uruguay"] = "乌拉圭",
        ["US"] = "美国",
        ["USA"] = "美国",
        ["Uzbekistan"] = "乌兹别克斯坦",
        ["Vanuatu"] = "瓦努阿图",
        ["Venezuela"] = "委内瑞拉",
        ["Viet Nam"] = "越南",
        ["Vietnam"] = "越南",
        ["W. Sahara"] = "西撒哈拉",
        ["West Bank"] = "西岸",
        ["Yemen"] = "也门",
        ["Zambia"] = "赞比亚",
        ["Zimbabwe"] = "津巴布韦",
    };

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

    /// <summary>
    ///     将已入库的国家名统一为中文（兼容历史英文数据）。
    /// </summary>
    public static string NormalizeCountryName(string? country)
    {
        if (string.IsNullOrEmpty(country) || country == "0") return Unknown;
        if (CountryZhMap.TryGetValue(country, out var zh)) return zh;
        return country;
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
            // 常见格式: 国家|区域|省份|城市|ISP，缺失字段为 0
            var region = searcher.Search(ip);
            if (string.IsNullOrEmpty(region)) return (Unknown, string.Empty);

            var parts = region.Split('|');
            var country = NormalizeCountryName(parts.Length > 0 ? parts[0] : null);
            var province = Normalize(parts.Length > 2 ? parts[2] : null);

            if (country == Unknown && province.Length > 0 && province != Unknown) country = China;
            if (province == Unknown) province = string.Empty;
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
