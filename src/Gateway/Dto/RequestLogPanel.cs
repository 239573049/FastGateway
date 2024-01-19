namespace Gateway.Dto;

public class RequestLogPanel
{
    public int Value { get; set; }
    
    public DateTime StartTime { get; set; }
    
    public string Time => StartTime.ToString("yyyy-MM-dd HH:mm:ss");
}