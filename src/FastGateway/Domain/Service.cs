using System.ComponentModel.DataAnnotations;
using FreeSql.DataAnnotations;

namespace FastGateway.Domain;

/// <summary>
/// 服务管理
/// </summary>
[Table(Name = "service")]
public class Service
{
    [Column(IsIdentity = true)]
    public string Id { get; set; }

    /// <summary>
    /// 服务端口
    /// </summary>
    public ushort Listen { get; set; }

    /// <summary>
    /// 是否HTTPS
    /// </summary>
    public bool IsHttps { get; set; }

    /// <summary>
    /// 启用黑名单
    /// </summary>
    public bool EnableBlacklist { get; set; }

    /// <summary>
    /// 启用白名单 （白名单优先级高，设置了白名单则忽略黑名单）
    /// </summary>
    public bool EnableWhitelist { get; set; }

    /// <summary>
    /// 是否启用流量监控
    /// </summary>
    public bool EnableFlowMonitoring { get; set; }

    /// <summary>
    /// 是否启用请求来源分析
    /// </summary>
    public bool EnableRequestSource { get; set; }

    /// <summary>
    /// 重定向到HTTPS
    /// </summary>
    public bool RedirectHttps { get; set; }

    /// <summary>
    /// 是否启动
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// 是否启用隧道
    /// </summary>
    public bool EnableTunnel { get; set; }

    /// <summary>
    /// 服务配置
    /// </summary>
    [Navigate(nameof(Location.ServiceId))]
    public virtual List<Location> Locations { get; set; }

    public string? RateLimitName { get; set; }

    [Column(IsIgnore = true)]
    public virtual RateLimit? RateLimit { get; set; }
}