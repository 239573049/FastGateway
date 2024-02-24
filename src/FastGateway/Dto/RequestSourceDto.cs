namespace FastGateway.Dto;

public sealed class RequestSourceDto
{
    public List<RequestSourceDayCountDto> DayCountDtos { get; set; }
    
    public List<RequestSourceEntity> Items { get; set; }

    public long Total { get; set; }
}

public sealed class RequestSourceDayCountDto
{
    public string? Day { get; set; }
    
    public long Count { get; set; }
}
