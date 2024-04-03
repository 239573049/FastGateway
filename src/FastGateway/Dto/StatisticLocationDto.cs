namespace FastGateway.Dto;

public sealed class StatisticLocationDto
{
    public int Count { get; set; }
    
    /// <summary>
    /// 归属地
    /// </summary>
    public string? Location { get; set; }

    public double Ratio { get; set; }
}