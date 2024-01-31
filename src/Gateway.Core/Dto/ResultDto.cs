namespace Gateway.Dto;

public class ResultDto
{
    public int Code { get; set; }

    public string Message { get; set; }
    
    public object? Data { get; set; }
    
    public static ResultDto Success(object? data = null)
    {
        return new()
        {
            Code = 0,
            Message = "success",
            Data = data
        };
    }
    
    public static ResultDto Error(string message, int code = -1)
    {
        return new()
        {
            Code = code,
            Message = message
        };
    }
}

public class ResultDto<T>
{
    public int Code { get; set; }

    public string Message { get; set; }
    
    public T? Data { get; set; }
    
    public static ResultDto<T> Success(T? data = default)
    {
        return new()
        {
            Code = 0,
            Message = "success",
            Data = data
        };
    }
    
    public static ResultDto<T> Error(string message, int code = -1)
    {
        return new()
        {
            Code = code,
            Message = message
        };
    }
}
