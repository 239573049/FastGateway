namespace Core.Entities;

public sealed class UpStream
{
    /// <summary>
    /// 服务
    /// </summary>
    public string Service { get; set; }

    /// <summary>
    /// 权重
    /// </summary>
    public int Weight { get; set; }
}