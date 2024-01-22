namespace Gateway.Entities;

public sealed class RouteEntity : Entity
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public string RouteId { get; set; } = null!;

    /// <summary>
    /// 路由名称
    /// </summary>
    public string RouteName { get; set; } = null!;

    /// <summary>
    /// 路由描述
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// 绑定集群Id可空
    /// </summary>
    public string? ClusterId { get; set; }
    
    /// <summary>
    /// 请求Body最大长度
    /// </summary>
    public long? MaxRequestBodySize { get; init; }

    [Column(MapType = typeof(string), StringLength = -1)]
    public RouteMatchEntity MatchEntities { get; set; } = new();
}

public sealed class RouteMatchEntity
{
    /// <summary>
    /// 路由配置
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 匹配域名
    /// </summary>
    public string[] Hosts { get; set; }
}