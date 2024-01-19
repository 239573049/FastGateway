namespace Gateway.Dto;

public class RequestLogPanel
{
    public List<RequestSizePanel> RequestSize { get; set; } = new();
    
    public List<RequestPathPanel> RequestPath { get; set; } = new();
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