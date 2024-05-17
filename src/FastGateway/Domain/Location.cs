namespace FastGateway.Domain;

/// <summary>
/// 服务节点管理
/// </summary>
[Table(Name = "location")]
[Index("location_serviceid", "ServiceId")]
public class Location
{
    public string Id { get; set; }

    /// <summary>
    /// 多个服务名称
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public string[] ServiceNames { get; set; }

    /// <summary>
    /// 绑定服务ID
    /// </summary>
    public string ServiceId { get; set; }

    /// <summary>
    /// 域名
    ///   --
    ///     多个路由
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public List<LocationService> LocationService { get; set; }
}

public sealed class LocationService
{
    /// <summary>
    /// 添加响应参数
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public Dictionary<string, string> AddHeader { get; set; } = new();

    /// <summary>
    /// 静态文件或目录
    /// </summary>
    public string? Root { get; set; }

    /// <summary>
    /// 路由
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 代理服务
    /// </summary>
    public string? ProxyPass { get; set; }

    /// <summary>
    /// 尝试按顺序匹配文件，如果未匹配到则返回 404
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public string[]? TryFiles { get; set; }

    public ApiServiceType Type { get; set; }

    /// <summary>
    /// 负载均衡模型
    /// </summary>
    public LoadType LoadType { get; set; } = LoadType.IpHash;


    [Column(MapType = typeof(string), StringLength = -1)]
    public List<UpStream> UpStreams { get; set; } = new();
}

public sealed class UpStream
{
    public string? Server { get; set; }

    /// <summary>
    /// 权重
    /// </summary>
    public int Weight { get; set; } = 1;
}