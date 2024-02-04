namespace FastGateway.Dto;

public class RouteDto
{
    public RouteDto(string routeId, string routeName, string? description, string? clusterId, long? maxRequestBodySize,
        string path, string[] hosts,
        ClusterEntity? clusterEntity)
    {
        RouteId = routeId;
        RouteName = routeName;
        Path = path;
        Hosts = hosts;
        Description = description;
        ClusterId = clusterId;
        MaxRequestBodySize = maxRequestBodySize;
        ClusterEntity = clusterEntity;
    }

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
    ///     请求Body最大长度
    /// </summary>
    public long? MaxRequestBodySize { get; init; }

    /// <summary>
    ///     绑定集群
    /// </summary>
    public ClusterEntity? ClusterEntity { get; set; }

    /// <summary>
    ///     路由配置
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    ///     匹配域名
    /// </summary>
    public string[] Hosts { get; set; }
}