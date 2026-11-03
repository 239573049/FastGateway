using System.Text.Json.Serialization;

namespace FastGateway.Risk.Model;

public sealed class HttpThreatFeatureState
{
    [JsonPropertyName("window_hours")] public int WindowHours { get; set; }
    [JsonPropertyName("requests")] public int Requests { get; set; }
    [JsonPropertyName("distinct_paths")] public int DistinctPaths { get; set; }
    [JsonPropertyName("distinct_hosts")] public int DistinctHosts { get; set; }
    [JsonPropertyName("not_found")] public int NotFound { get; set; }
    [JsonPropertyName("not_found_ratio")] public double NotFoundRatio { get; set; }
    [JsonPropertyName("four_xx")] public int FourXx { get; set; }
    [JsonPropertyName("four_xx_ratio")] public double FourXxRatio { get; set; }
    [JsonPropertyName("blocked_requests")] public int BlockedRequests { get; set; }
    [JsonPropertyName("suspicious_path_hits")] public int SuspiciousPathHits { get; set; }
    [JsonPropertyName("max_requests_per_minute")] public int MaxRequestsPerMinute { get; set; }
    [JsonPropertyName("methods")] public List<string> Methods { get; set; } = [];
    [JsonPropertyName("top_hosts")] public List<string> TopHosts { get; set; } = [];
    [JsonPropertyName("top_paths")] public List<string> TopPaths { get; set; } = [];
    [JsonPropertyName("request_targets")] public List<HttpThreatRequestTarget> RequestTargets { get; set; } = [];
    [JsonPropertyName("suspicious_path_examples")] public List<string> SuspiciousPathExamples { get; set; } = [];

    /// <summary>"ip" 或 "ipv6_64"（同一 /64 网段内的多个地址聚合为一个来源）。</summary>
    [JsonPropertyName("source_kind")] public string SourceKind { get; set; } = "ip";

    [JsonPropertyName("distinct_ips")] public int DistinctIps { get; set; } = 1;

    /// <summary>UA 自称的搜索引擎爬虫名；仅在校验未通过或未能校验时出现（通过校验的不会提交）。</summary>
    [JsonPropertyName("claimed_crawler")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClaimedCrawler { get; set; }

    /// <summary>"failed"：反向 DNS 与官方域名不符（仿冒）；"unknown"：DNS 不可用或超时。</summary>
    [JsonPropertyName("crawler_verification")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CrawlerVerification { get; set; }

    /// <summary>该来源此前的判断记录；首次出现时为空。</summary>
    [JsonPropertyName("history")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HttpThreatHistory? History { get; set; }
}

/// <summary>
///     跨窗口的历史摘要：每轮只提交水位线之后的新请求，低频分散的探测在单轮里特征很弱，需要历史补全上下文。
/// </summary>
public sealed class HttpThreatHistory
{
    [JsonPropertyName("window_days")] public int WindowDays { get; set; }
    [JsonPropertyName("prior_evaluations")] public int PriorEvaluations { get; set; }
    [JsonPropertyName("prior_requests")] public long PriorRequests { get; set; }
    [JsonPropertyName("prior_max_risk")] public double PriorMaxRisk { get; set; }
    [JsonPropertyName("last_risk")] public double LastRisk { get; set; }
    [JsonPropertyName("hours_since_last_evaluation")] public double HoursSinceLastEvaluation { get; set; }
    [JsonPropertyName("prior_bans")] public int PriorBans { get; set; }
    [JsonPropertyName("prior_suspicious_paths")] public List<string> PriorSuspiciousPaths { get; set; } = [];
}

public sealed class HttpThreatRequestTarget
{
    [JsonPropertyName("host")] public string Host { get; set; } = string.Empty;
    [JsonPropertyName("method")] public string Method { get; set; } = string.Empty;
    [JsonPropertyName("path")] public string Path { get; set; } = string.Empty;
    [JsonPropertyName("status")] public int Status { get; set; }
    [JsonPropertyName("count")] public int Count { get; set; }
}

public sealed class JevHttpThreatRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
    [JsonPropertyName("state")] public HttpThreatFeatureState State { get; set; } = new();
    [JsonPropertyName("questions")] public Dictionary<string, JevQuestion> Questions { get; set; } = new();
}
