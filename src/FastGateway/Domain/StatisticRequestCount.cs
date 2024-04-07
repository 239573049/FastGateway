namespace FastGateway.Domain;

[Table(Name = "statistic_request_count")]
public sealed class StatisticRequestCount
{
    [Column(IsIdentity = true)]
    public long Id { get; set; }
    
    public string ServiceId { get; set; }
    
    public int RequestCount { get; set; }
    
    public int Error4xxCount { get; set; }
    
    public int Error5xxCount { get; set; }
    
    /// <summary>
    /// 年
    /// </summary>
    public ushort Year { get; set; }

    /// <summary>
    /// 月
    /// </summary>
    public byte Month { get; set; }

    /// <summary>
    /// 天
    /// </summary>
    public byte Day { get; set; }
    
    [Navigate(nameof(ServiceId))]
    public Service Service { get; set; }
}