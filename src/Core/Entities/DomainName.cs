using Core.Entities.Core;
using MessagePack;

namespace Core.Entities;

/// <summary>
/// 域名解析
/// </summary>
[MessagePackObject(true)]
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
    /// 请求服务使用的Host
    /// </summary>
    /// <returns></returns>
    public string? Host { get; set; }

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
    /// 是否启用浏览器机器人保护（Cloudflare Turnstile）
    /// </summary>
    public bool EnableBotProtection { get; set; }

    /// <summary>
    /// 登录风控（暴力破解、撞库防护）
    /// </summary>
    public RiskProtection? RiskProtection { get; set; }

    /// <summary>
    /// 服务
    /// </summary>
    public string? Service { get; set; }

    /// <summary>
    /// 集群
    /// </summary>
    public List<UpStream> UpStreams { get; set; } = new();

    /// <summary>
    /// 是否启用健康检查
    /// </summary>
    public bool EnableHealthCheck { get; set; }

    /// <summary>
    /// 健康检查地址（路径），例如：/health 或 /api/health?ready=1
    /// </summary>
    public string? HealthCheckPath { get; set; }

    /// <summary>
    /// 健康检查间隔（秒）
    /// </summary>
    public int HealthCheckIntervalSeconds { get; set; } = 10;

    /// <summary>
    /// 健康检查超时（秒）
    /// </summary>
    public int HealthCheckTimeoutSeconds { get; set; } = 3;

    /// <summary>
    /// 集群模式下指定由哪个节点访问上游：null/空 = 收到请求的节点直连（默认，保持原有行为）；
    /// "master" = 主网关；其他值 = 从节点 Id。请求到达非指定节点时自动中继给指定节点。
    /// </summary>
    public string? AccessNodeId { get; set; }

    /// <summary>
    /// 静态文件或目录
    /// </summary>
    public string? Root { get; set; }

    /// <summary>
    /// TryFiles
    /// </summary>
    public string[] TryFiles { get; set; } = [];
}
