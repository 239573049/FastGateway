using System.Text.Json.Serialization;
using FastGateway.Services.Statistics;

namespace FastGateway.Risk.Model;

/// <summary>
///     发给模型的特征。只允许数字、布尔值和固定枚举：攻击者可控的原始字符串（UA、用户名、路径）一律不发，
///     既避免借请求内容影响模型判断，也避免把 IP、账号等个人信息发往第三方
/// </summary>
public sealed class RiskFeatureState
{
    [JsonPropertyName("subject_type")] public string SubjectType { get; set; } = "ip";

    [JsonPropertyName("window_minutes")] public int WindowMinutes { get; set; } = 10;

    [JsonPropertyName("login_attempts")] public int LoginAttempts { get; set; }

    [JsonPropertyName("login_failures")] public int LoginFailures { get; set; }

    [JsonPropertyName("failure_ratio")] public double FailureRatio { get; set; }

    [JsonPropertyName("distinct_accounts")] public int DistinctAccounts { get; set; }

    [JsonPropertyName("subnet_failures")] public int SubnetFailures { get; set; }

    [JsonPropertyName("median_interval_ms")] public long? MedianIntervalMs { get; set; }

    [JsonPropertyName("interval_stddev_ms")] public long? IntervalStddevMs { get; set; }

    /// <summary>browser / script_library / cli_tool / empty / unknown</summary>
    [JsonPropertyName("client_kind")] public string ClientKind { get; set; } = ClientKinds.Unknown;

    [JsonPropertyName("sec_fetch_present")] public bool SecFetchPresent { get; set; }

    [JsonPropertyName("accept_language_present")] public bool AcceptLanguagePresent { get; set; }

    /// <summary>consumer / hosting / intranet / unknown</summary>
    [JsonPropertyName("network_type")] public string NetworkType { get; set; } = "unknown";

    [JsonPropertyName("previous_blocks_24h")] public int PreviousBlocks24h { get; set; }

    [JsonPropertyName("rule_score")] public int RuleScore { get; set; }
}

public static class ClientKinds
{
    public const string Browser = "browser";
    public const string ScriptLibrary = "script_library";
    public const string CliTool = "cli_tool";
    public const string Empty = "empty";
    public const string Unknown = "unknown";
}

public static class NetworkTypes
{
    // ip2region 的 ISP 字段只有运营商/云厂商名称，粗分为机房与家庭/移动网络
    private static readonly string[] HostingMarkers =
    [
        "阿里云", "腾讯", "华为云", "百度云", "金山云", "京东云", "UCloud", "青云", "天翼云", "移动云", "联通云",
        "Amazon", "AWS", "Google", "Microsoft", "Azure", "DigitalOcean", "Linode", "Akamai", "Vultr", "OVH",
        "Hetzner", "Oracle", "Cloudflare", "IDC", "数据中心", "机房"
    ];

    private static readonly string[] ConsumerMarkers = ["电信", "联通", "移动", "广电", "铁通", "教育网", "鹏博士", "长城宽带"];

    public static string Resolve(string ip)
    {
        var (country, _) = GeoIpService.Resolve(ip);
        if (country == GeoIpService.Intranet) return "intranet";

        var isp = GeoIpService.ResolveIsp(ip);
        if (string.IsNullOrEmpty(isp)) return "unknown";
        if (HostingMarkers.Any(x => isp.Contains(x, StringComparison.OrdinalIgnoreCase))) return "hosting";
        if (ConsumerMarkers.Any(x => isp.Contains(x, StringComparison.Ordinal))) return "consumer";
        return "unknown";
    }
}
