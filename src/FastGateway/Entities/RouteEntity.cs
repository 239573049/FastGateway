namespace FastGateway.Entities;

public sealed class RouteEntity : Entity
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public string RouteId { get; set; } = null!;

    /// <summary>
    ///     路由名称
    /// </summary>
    public string RouteName { get; set; } = null!;

    /// <summary>
    ///     路由描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     绑定集群Id可空
    /// </summary>
    public string? ClusterId { get; set; }

    /// <summary>
    ///     授权策略
    /// </summary>
    public string? AuthorizationPolicy { get; set; }

    /// <summary>
    ///     认证中心否需要https
    /// </summary>
    public bool? RequireHttpsMetadata { get; set; }

    /// <summary>
    ///     授权认证中心地址
    /// </summary>
    public string? AuthorizationPolicyAddress { get; set; }

    /// <summary>
    ///     请求Body最大长度
    /// </summary>
    public long? MaxRequestBodySize { get; set; }

    /// <summary>
    ///     路由配置
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    ///     匹配域名
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public string[] Hosts { get; set; } = Array.Empty<string>();
}