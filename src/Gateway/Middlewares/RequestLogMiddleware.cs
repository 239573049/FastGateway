namespace Gateway.Middlewares;

/// <summary>
/// 用于记录请求日志的中间件
/// </summary>
public class RequestLogMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (RequestOptions.FilterSuffixes.Any(x => context.Request.Path.Value?.EndsWith(x) == true) ||
            context.Request.Path.Value?.Contains("/api/gateway") == true)
        {
            await next(context);
            return;
        }

        var requestLogService = context.RequestServices.GetRequiredService<RequestLogService>();
        var ip = context.Connection.RemoteIpAddress?.ToString();

        var requestLog = new RequestLog
        {
            Path = context.Request.Path,
            Method = context.Request.Method,
            QueryString = context.Request.QueryString.ToString(),
            CreatedTime = DateTime.Now,
            Ip = ip,
            BrowserInfo = context.Request.Headers.UserAgent.ToString(),
        };
        var stopwatch = Stopwatch.StartNew();
        await next(context);
        stopwatch.Stop();

        // 过滤Content-Type
        if (RequestOptions.FilterContentTypes.Any(x => context.Response.ContentType?.Contains(x) == true))
        {
            return;
        }

        requestLog.ExecutionDuration = stopwatch.Elapsed.TotalMilliseconds;
        requestLog.StatusCode = context.Response.StatusCode;
        await requestLogService.LogAsync(requestLog);
    }
}