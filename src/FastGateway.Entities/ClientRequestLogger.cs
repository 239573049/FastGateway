namespace FastGateway.Entities;

public class ClientRequestLogger
{
    public long Id { get; set; }
    
    public string Ip { get; set; }

    public int Total { get; set; }

    public int Success { get; set; }

    public int Fail { get; set; }
    
    public string RequestTime { get; set; }
    
    /// <summary>
    /// 请求国家
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// 请求地区
    /// </summary>
    public string? Region { get; set; }

}