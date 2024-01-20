namespace Gateway.Dto;

public class RequestLogPanel
{
    public List<RequestSizePanel> RequestSize { get; set; } = new();

    public List<RequestPathPanel> RequestPath { get; set; } = new();

    public List<object> RequestStatusCode { get; set; } = new();
}

public class RequestSizePanel
{
    public int Value { get; set; }

    public DateTime StartTime { get; set; }

    public string Time => StartTime.ToString("yyyy-MM-dd HH:mm:ss");
}

public class RequestPathPanel
{
    public int Value { get; set; }

    public string Type { get; set; }

    public int Id { get; set; }
}

public class RequestStatusCodePanel
{
    public DateTime CreatedTime { get; set; }

    /// <summary>
    /// 正常请求
    /// </summary>
    public int NormalRequests { get; set; }

    /// <summary>
    /// 特殊请求
    /// </summary>
    public int ExceptionalRequests { get; set; }

    /// <summary>
    /// 异常请求
    /// </summary>
    public int ErrorRequests { get; set; }
}