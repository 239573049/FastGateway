namespace FastGateway.Dto;

public class ServerDto
{
    public string Id { get; set; }

    /// <summary>
    ///     服务端口
    /// </summary>
    public ushort Listen { get; set; }

    /// <summary>
    ///     服务名称
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    ///     服务描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    ///     重定向到HTTPS
    /// </summary>
    public bool RedirectHttps { get; set; }

    /// <summary>
    ///     是否启动
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    ///     静态资源压缩
    /// </summary>
    public bool StaticCompress { get; set; }

    /// <summary>
    ///     是否HTTPS
    /// </summary>
    public bool IsHttps { get; set; }

    /// <summary>
    ///     是否启用隧道
    /// </summary>
    public bool EnableTunnel { get; set; }

    /// <summary>
    ///     启用黑名单
    /// </summary>
    public bool EnableBlacklist { get; set; }

    /// <summary>
    ///     启用白名单 （白名单优先级高，设置了白名单则忽略黑名单）
    /// </summary>
    public bool EnableWhitelist { get; set; }

    public bool OnLine { get; set; }

    /// <summary>
    ///     是否复制请求域名
    /// </summary>
    public bool CopyRequestHost { get; set; }
}