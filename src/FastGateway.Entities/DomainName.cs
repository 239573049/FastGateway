using FastGateway.Entities.Core;

namespace FastGateway.Entities;

/// <summary>
/// 域名解析
/// </summary>
public sealed class DomainName
{
    /// <summary>
    /// 主键ID
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// 路由代理
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// 服务ID
    /// </summary>
    public string ServerId { get; set; }

    /// <summary>
    /// 域名
    /// </summary>
    public string[] Domains { get; set; } = [];

    /// <summary>
    /// 服务类型
    /// </summary>
    public ServiceType ServiceType { get; set; }

    /// <summary>
    /// Header
    /// </summary>
    public List<HeadersView> Headers { get; set; } = new();

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// 服务
    /// </summary>
    public string? Service { get; set; }

    /// <summary>
    /// 集群
    /// </summary>
    public List<UpStream> UpStreams { get; set; } = new();

    /// <summary>
    /// 静态文件或目录
    /// </summary>
    public string? Root { get; set; }
}