namespace FastGateway.Dto;

public sealed class StatisticLocationDto
{
    public decimal Count { get; set; }
    
    /// <summary>
    /// 归属地
    /// </summary>
    public string? Location { get; set; }
    
    /// <summary>
    /// 省份
    /// </summary>
    public string? Province { get; set; }
    
    /// <summary>
    /// 国家
    /// </summary>
    public string? Country { get; set; }

    public decimal Ratio { get; set; }
}