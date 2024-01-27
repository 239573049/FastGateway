namespace Gateway.Entities;

public sealed class StaticFileProxyEntity : Entity
{
    /// <summary>
    /// 代理名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 代理描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 匹配路由
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 文件目录
    /// </summary>
    public string Root { get; set; }
    
    /// <summary>
    /// 匹配域名（如果配置了域名则优先域名在到路由）
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public string[] Hosts { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 是否启用GZIP压缩
    /// </summary>
    public bool GZip { get; set; }

    /// <summary>
    /// 设置相应头
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public Dictionary<string,string> ResponseHeaders { get; set; } = new();

    /// <summary>
    /// 默认加载文件
    /// </summary>
    public string? Index { get; set; }

    /// <summary>
    /// 此参数用于尝试的文件列表，如果找不到请求的文件，则会尝试下一个。
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public string[] TryFiles { get; set; }
}