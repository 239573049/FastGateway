namespace FastGateway.Middlewares;

public sealed class GatewayMiddleware(RequestSourceService requestSourceService) : IMiddleware
{
    /// <summary>
    ///     请求计数器
    /// </summary>
    private static int _currentRequestCount;

    /// <summary>
    ///     异常计数器
    /// </summary>
    private static int _currentErrorCount;

    public static int CurrentRequestCount => _currentRequestCount;

    public static int CurrentErrorCount => _currentErrorCount;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // TODO: 由于h3需要对应请求的端口，所以这里需要动态设置
        context.Response.Headers.AltSvc = "h3=\":" + context.Request.Host.Port + "\"";

        // 添加X-Forwarded-For头，用于获取真实的客户端IP
        var ip = context.Connection.RemoteIpAddress?.MapToIPv4()?.ToString();

        context.Request.Headers["X-Forwarded-For"] = ip;

        context.Response.Headers["FastGateway-Version"] = GatewayOptions.Version;

        Interlocked.Increment(ref _currentRequestCount);

        // 获取当前请求平台
        requestSourceService.Execute(new RequestSourceEntity()
        {
            CreatedTime = DateTime.Now,
            Ip = ip,
            Host = context.Request.Host.Host,
            Platform = GetUserPlatform(context.Request.Headers.UserAgent.ToString()),
            Id = Guid.NewGuid().ToString("N"),
        });

        await next(context);

        if (context.Response.StatusCode >= 400) Interlocked.Increment(ref _currentErrorCount);
    }

    private static string GetUserPlatform(string userAgent)
    {
        if (userAgent.Contains("Windows NT"))
        {
            return "Windows";
        }

        if (userAgent.Contains("Macintosh"))
        {
            return "Mac";
        }

        return userAgent.Contains("Linux") ? "Linux" : "Unknown";
    }

    public static void ClearRequestCount()
    {
        Interlocked.Exchange(ref _currentRequestCount, 0);
        Interlocked.Exchange(ref _currentErrorCount, 0);
    }
}