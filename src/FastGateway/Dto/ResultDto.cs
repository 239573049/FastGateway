namespace FastGateway.Dto;

public class ResultDto
{
    public bool Success { get; set; }

    public string? Message { get; set; }

    public object? Data { get; set; }
    
    public static ResultDto SuccessResult(object? data = null)
    {
        return new ResultDto
        {
            Success = true,
            Data = data,
        };
    }
    
    public static ResultDto ErrorResult(string message)
    {
        return new ResultDto
        {
            Success = false,
            Message = message
        };
    }
    
    public static ResultDto ErrorResult(Exception exception)
    {
        return new ResultDto
        {
            Success = false,
            Message = exception.Message
        };
    }
    
    
}

public class ResultDto<T>
{
    public bool Success { get; set; }

    public string? Message { get; set; }

    public T? Data { get; set; }
    
    public static ResultDto<T> SuccessResult(T? data = default)
    {
        return new ResultDto<T>
        {
            Success = true,
            Data = data
        };
    }
    
    public static ResultDto<T> ErrorResult(string message)
    {
        return new ResultDto<T>
        {
            Success = false,
            Message = message
        };
    }
    
    public static ResultDto<T> ErrorResult(Exception exception)
    {
        return new ResultDto<T>
        {
            Success = false,
            Message = exception.Message
        };
    }
}