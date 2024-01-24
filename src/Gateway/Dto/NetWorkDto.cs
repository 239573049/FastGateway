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
}