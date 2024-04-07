namespace FastGateway.Dto;

public class LocationInput
{
    public string? Id { get; set; }
    
    /// <summary>
    /// 绑定服务ID
    /// </summary>
    public string ServiceId { get; set; }

    /// <summary>
    /// 多个服务名称
    /// </summary>
    public string[] ServiceNames { get; set; }

    public List<LocationServiceDto> LocationService { get; set; } = new();
}
public sealed class LocationServiceDto
{
    /// <summary>
    /// 添加请求头参数
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
    public string[]? TryFiles { get; set; }

    public ApiServiceType Type { get; set; }

    /// <summary>
    /// 负载均衡模型
    /// </summary>
    public LoadType LoadType { get; set; } = LoadType.IpHash;


    [Column(MapType = typeof(string), StringLength = -1)]
    public List<UpStreamInput> UpStreams { get; set; } = new();
}

public class UpStreamInput
{
    public string? Server { get; set; }

    /// <summary>
    /// 权重
    /// </summary>
    public int Weight { get; set; } = 1;
}