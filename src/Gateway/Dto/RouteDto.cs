namespace Gateway.Dto;

public class RouteDto
{
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

    public RouteMatchDto MatchEntities { get; set; } = new();

    /// <summary>
    /// 绑定集群
    /// </summary>
    public ClusterEntity? ClusterEntity { get; set; }

    public RouteDto(string routeId, string routeName, string? description, string? clusterId, long? maxRequestBodySize,
        RouteMatchDto matchEntities, ClusterEntity? clusterEntity)
    {
        RouteId = routeId;
        RouteName = routeName;
        Description = description;
        ClusterId = clusterId;
        MaxRequestBodySize = maxRequestBodySize;
        MatchEntities = matchEntities;
        ClusterEntity = clusterEntity;
    }
}

public class RouteMatchDto
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