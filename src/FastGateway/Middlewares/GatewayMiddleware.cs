namespace FastGateway.Middlewares;

public class GatewayMiddleware : IMiddleware
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

        // Gateway默认的请求不记录
        var record = context.Request.Path.Value?.StartsWith("/api/gateway/") == false;

        // 使用原子操作，防止并发问题
        if (record)
            Interlocked.Increment(ref _currentRequestCount);

        await next(context);

        if (record && context.Response.StatusCode >= 400) Interlocked.Increment(ref _currentErrorCount);
    }

    public static void ClearRequestCount()
    {
        Interlocked.Exchange(ref _currentRequestCount, 0);
        Interlocked.Exchange(ref _currentErrorCount, 0);
    }
}