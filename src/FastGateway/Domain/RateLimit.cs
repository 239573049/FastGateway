namespace FastGateway.Domain;

public sealed class RateLimit
{
    /// <summary>
    /// 限流策略名称
    /// </summary>
    [Column(IsIdentity = true)]
    public string Name { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// 通用规则列表
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public List<GeneralRules> GeneralRules { get; set; }

    /// <summary>
    /// 端点白名单
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public List<string> EndpointWhitelist { get; set; }

    /// <summary>
    /// 客户端ID头部
    /// </summary>
    public string ClientIdHeader { get; set; } = "X-ClientId";

    /// <summary>
    /// 客户端白名单
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public List<string> ClientWhitelist { get; set; }

    /// <summary>
    /// 真实IP头部
    /// </summary>
    public string RealIpHeader { get; set; } = "X-Real-IP";

    /// <summary>
    /// IP白名单
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public List<string> IpWhitelist { get; set; }

    /// <summary>
    /// HTTP状态码
    /// </summary>
    public int HttpStatusCode { get; set; } = 429;

    /// <summary>
    /// 超出配额消息
    /// </summary>
    public string QuotaExceededMessage { get; set; }

    /// <summary>
    /// 错误内容类型
    /// </summary>
    public string RateLimitContentType { get; set; } = "text/html";

    /// <summary>
    /// 速率限制计数器前缀
    /// </summary>
    public string RateLimitCounterPrefix { get; set; } = "crlc";

    /// <summary>
    /// 启用端点速率限制
    /// </summary>
    public bool EnableEndpointRateLimiting { get; set; }

    /// <summary>
    /// 禁用速率限制头部
    /// </summary>
    public bool DisableRateLimitHeaders { get; set; }

    /// <summary>
    /// 启用正则规则匹配
    /// </summary>
    public bool EnableRegexRuleMatching { get; set; }
}

public class GeneralRules
{
    public string Endpoint { get; set; }
    public string Period { get; set; }
    public int Limit { get; set; }
}