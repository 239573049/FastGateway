namespace Gateway.Core.Dto;

public class NetWorkDto(long received, long sent)
{
    /// <summary>
    /// 接收流量
    /// </summary>
    public long Received { get; set; } = received;


    /// <summary>
    /// 发送流量
    /// </summary>
    public long Sent { get; set; } = sent;


    /// <summary>
    /// 当前时间
    /// </summary>
    public string Time => DateTime.Now.ToString("HH:mm:ss");

    /// <summary>
    /// 当天请求数量
    /// </summary>
    public int CurrentRequestCount { get; set; }

    /// <summary>
    /// 当天错误数量
    /// </summary>
    public int CurrentErrorCount { get; set; }

    private double _totalRequestCount;

    /// <summary>
    /// 当天错误率
    /// </summary>
    public double TotalRequestCount
    {
        get => _totalRequestCount + CurrentRequestCount;
        set => _totalRequestCount = value;
    }

    private double _totalErrorCount;

    /// <summary>
    /// 总错误数量
    /// </summary>
    public double TotalErrorCount
    {
        get => _totalErrorCount + CurrentErrorCount;
        set => value = _totalErrorCount;
    }


    /// <summary>
    /// 获取读取速率
    /// </summary>
    public double ReadRate { get; init; }

    /// <summary>
    /// 获取写入速率
    /// </summary>
    public double WriteRate { get; init; }

    private double _totalRead;

    /// <summary>
    /// 获取总读上行
    /// </summary>
    public double TotalRead
    {
        get => (double)((decimal)_totalRead + (decimal)ReadRate);
        init => _totalRead = value;
    }

    private double _totalWrite;

    /// <summary>
    /// 获取总下行
    /// </summary>
    public double TotalWrite
    {
        get => (double)((decimal)_totalWrite + (decimal)WriteRate);
        init => _totalWrite = value;
    }
}