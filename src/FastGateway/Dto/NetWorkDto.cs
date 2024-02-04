namespace FastGateway.Dto;

public class NetWorkDto(long received, long sent)
{
    private double _totalErrorCount;

    private readonly double _totalRead;

    private double _totalRequestCount;

    private readonly double _totalWrite;

    /// <summary>
    ///     接收流量
    /// </summary>
    public long Received { get; set; } = received;

    /// <summary>
    ///     接收流量字符串
    /// </summary>
    public string ReceivedStr => StringHelper.FormatBytes(Received);

    /// <summary>
    ///     发送流量
    /// </summary>
    public long Sent { get; set; } = sent;

    /// <summary>
    ///     发送流量字符串
    /// </summary>
    public string SentStr => StringHelper.FormatBytes(Sent);

    /// <summary>
    ///     当前时间
    /// </summary>
    public string Time => DateTime.Now.ToString("HH:mm:ss");

    /// <summary>
    ///     当天请求数量
    /// </summary>
    public int CurrentRequestCount { get; set; } = GatewayMiddleware.CurrentRequestCount;

    /// <summary>
    ///     当天错误数量
    /// </summary>
    public int CurrentErrorCount { get; set; } = GatewayMiddleware.CurrentErrorCount;

    /// <summary>
    ///     当天错误率
    /// </summary>
    public double TotalRequestCount
    {
        get => _totalRequestCount + CurrentRequestCount;
        set => _totalRequestCount = value;
    }

    /// <summary>
    ///     总错误数量
    /// </summary>
    public double TotalErrorCount
    {
        get => _totalErrorCount + CurrentErrorCount;
        set => value = _totalErrorCount;
    }


    /// <summary>
    ///     获取读取速率
    /// </summary>
    public double ReadRate { get; init; }

    /// <summary>
    ///     获取写入速率
    /// </summary>
    public double WriteRate { get; init; }

    /// <summary>
    ///     获取总读上行
    /// </summary>
    public double TotalRead
    {
        get => (double)((decimal)_totalRead + (decimal)ReadRate);
        init => _totalRead = value;
    }

    /// <summary>
    ///     获取总下行
    /// </summary>
    public double TotalWrite
    {
        get => (double)((decimal)_totalWrite + (decimal)WriteRate);
        init => _totalWrite = value;
    }
}