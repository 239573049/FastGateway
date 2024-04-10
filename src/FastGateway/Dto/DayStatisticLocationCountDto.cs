namespace FastGateway.Dto;

public sealed class DayStatisticLocationCountDto
{
    public string Ip { get; set; }

    public decimal Count { get; set; }
    
    public string? Location { get; set; }
}