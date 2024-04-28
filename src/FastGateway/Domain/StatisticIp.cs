namespace FastGateway.Domain;

[Table(Name = "statistic_ip")]
[Index("statistic_ip_year", "Year")]
[Index("statistic_ip_month", "Month")]
[Index("statistic_ip_day", "Day")]
public sealed class StatisticIp
{
    /// <summary>
    /// 获取或设置标识列。
    /// </summary>
    [Column(IsIdentity = true)]
    public long Id { get; set; }

    /// <summary>
    /// 获取或设置IP地址。
    /// </summary>
    public string Ip { get; set; }

    /// <summary>
    /// 获取或设置出现的次数。
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// 获取或设置记录的年份。
    /// </summary>
    public ushort Year { get; set; }

    /// <summary>
    /// 获取或设置记录的月份。
    /// </summary>
    public byte Month { get; set; }

    /// <summary>
    /// 获取或设置记录的日期。
    /// </summary>
    public byte Day { get; set; }

    /// <summary>
    /// 获取或设置IP地址的位置。
    /// 如果位置未知，值可以为空。
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// 获取或设置与记录关联的服务ID。
    /// </summary>
    public string ServiceId { get; set; }
}