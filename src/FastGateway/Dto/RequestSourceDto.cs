namespace FastGateway.Dto;

public sealed class RequestSourceDto
{
    public List<RequestSourceAddressDto> AddressDtos { get; set; }
    
    public List<RequestSourceDayCountDto> DayCountDtos { get; set; }
}

public sealed class RequestSourceDayCountDto
{
    public string? Day { get; set; }
    
    public long Count { get; set; }
}

public sealed class RequestSourceAddressDto
{
    /// <summary>
    /// 来源地址
    /// </summary>
    public string? HomeAddress { get; set; }

    /// <summary>
    /// 来源IP总数
    /// </summary>
    public long Count { get; set; }
}
