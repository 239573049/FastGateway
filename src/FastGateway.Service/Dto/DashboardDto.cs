namespace FastGateway.Service.Dto;

public class DashboardDto
{
    public long Total { get; set; }

    public long Success { get; set; }

    public long Fail { get; set; }

    /// <summary>
    /// 今天总数
    /// </summary>
    public long TodayTotal { get; set; }

    /// <summary>
    /// 今天成功数
    /// </summary>
    public long TodaySuccess { get; set; }

    /// <summary>
    /// 今天失败数
    /// </summary>
    public long TodayFail { get; set; }
}