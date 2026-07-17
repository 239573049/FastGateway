namespace Core.Entities;

/// <summary>
/// L4 端口转发上游目标
/// </summary>
public sealed class StreamUpStream
{
    /// <summary>
    /// 上游地址（IP 或域名）
    /// </summary>
    public string Host { get; set; } = null!;

    /// <summary>
    /// 上游端口
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// 权重（预留，用于加权负载均衡）
    /// </summary>
    public int Weight { get; set; } = 1;
}
