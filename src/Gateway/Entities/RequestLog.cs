namespace Gateway.Entities;

[Index("uk_path", nameof(Path), false)]
[Index("uk_ip", nameof(Ip), false)]
public sealed class RequestLog : Entity
{
    public string Path { get; set; }
    
    public string Method { get; set; }
    
    public string QueryString { get; set; }

    /// <summary>
    /// 请求IP
    /// </summary>
    public string? Ip { get; set; }

    /// <summary>
    /// 目标地址
    /// </summary>
    public string? Target { get; set; }

    /// <summary>
    /// 状态码
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// 执行时长（ms）
    /// </summary>
    public double ExecutionDuration { get; set; }

    /// <summary>
    /// 请求域名
    /// </summary>
    public string? Host { get; set; }
    
    /// <summary>
    /// 浏览器信息
    /// </summary>
    public string BrowserInfo { get; set; }

    /// <summary>
    /// 扩展属性
    /// </summary>
    [Column(MapType = typeof(string), StringLength = -1)]
    public Dictionary<string,string> ExtraProperties { get; set; } = new();
}