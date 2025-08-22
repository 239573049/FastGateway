namespace FastGateway.Dto;

public sealed class ResultDto
{
    public ResultDto(bool success, string message, object data)
    {
        Success = success;
        Message = message;
        Data = data;
    }

    public bool Success { get; set; }

    public string Message { get; set; }

    public object Data { get; set; }

    public static ResultDto CreateSuccess(string message, object data)
    {
        return new ResultDto(true, message, data);
    }

    public static ResultDto CreateSuccess(string message)
    {
        return new ResultDto(true, message, null);
    }

    public static ResultDto CreateSuccess(object data)
    {
        return new ResultDto(true, null, data);
    }

    public static ResultDto CreateSuccess()
    {
        return new ResultDto(true, null, null);
    }

    public static ResultDto CreateFailed(string message, object data)
    {
        return new ResultDto(false, message, data);
    }

    public static ResultDto CreateFailed(string message)
    {
        return new ResultDto(false, message, null);
    }
}

public sealed class ResultDto<T>(bool success, string message, T data)
{
    public bool Success { get; set; } = success;

    public string Message { get; set; } = message;

    public T Data { get; set; } = data;

    public static ResultDto<T> CreateSuccess(string message, T data)
    {
        return new ResultDto<T>(true, message, data);
    }

    public static ResultDto<T> CreateSuccess(string message)
    {
        return new ResultDto<T>(true, message, default);
    }

    public static ResultDto<T> CreateSuccess(T data)
    {
        return new ResultDto<T>(true, null, data);
    }

    public static ResultDto<T> CreateSuccess()
    {
        return new ResultDto<T>(true, null, default);
    }

    public static ResultDto<T> CreateFailed(string message, T data)
    {
        return new ResultDto<T>(false, message, data);
    }

    public static ResultDto<T> CreateFailed(string message)
    {
        return new ResultDto<T>(false, message, default);
    }
}