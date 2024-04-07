namespace FastGateway.Dto;

public sealed class StatisticLocationDto
{
    public decimal Count { get; set; }
    
    /// <summary>
    /// 归属地
    /// </summary>
    public string? Location { get; set; }

    public decimal Ratio { get; set; }
}