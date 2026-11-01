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
