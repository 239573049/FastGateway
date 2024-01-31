using Gateway.Helpers;

namespace Gateway.Dto;

public class NetWorkDto(long received, long sent)
{
    /// <summary>
    /// 接收流量
    /// </summary>
    public long Received { get; set; } = received;

    /// <summary>
    /// 接收流量字符串
    /// </summary>
    public string ReceivedStr => StringHelper.FormatBytes(Received);

    /// <summary>
    /// 发送流量
    /// </summary>
    public long Sent { get; set; } = sent;

    /// <summary>
    /// 发送流量字符串
    /// </summary>
    public string SentStr => StringHelper.FormatBytes(Sent);

    /// <summary>
    /// 当前时间
    /// </summary>
    public string Time => DateTime.Now.ToString("HH:mm:ss");

    /// <summary>
    /// 当天请求数量
    /// </summary>
    public int CurrentRequestCount { get; set; } = GatewayMiddleware.CurrentRequestCount;

    /// <summary>
    /// 当天错误数量
    /// </summary>
    public int CurrentErrorCount { get; set; } = GatewayMiddleware.CurrentErrorCount;

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
}