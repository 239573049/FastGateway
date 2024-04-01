using FastGateway.BackgroundServices;

namespace FastGateway.Middlewares;

public sealed class StatisticsMiddleware(ICurrentContext currentContext) : IMiddleware
{
    private static int _requestCount;
    private static int _error4xx;
    private static int _error5xx;

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        await next(context);


        switch (context.Response.StatusCode)
        {
            case >= 400 and < 500:
                StatisticsBackgroundService.WriteAsync(new StatisticRequestCountDto()
                {
                    CreatedTime = DateTime.Now,
                    Error4xxCount = 1,
                    Error5xxCount = 0,
                    RequestCount = 1,
                });
                break;
            case >= 500:
                StatisticsBackgroundService.WriteAsync(new StatisticRequestCountDto()
                {
                    CreatedTime = DateTime.Now,
                    Error4xxCount = 0,
                    Error5xxCount = 1,
                    RequestCount = 1,
                });
                break;
            case < 400:
                StatisticsBackgroundService.WriteAsync(new StatisticRequestCountDto()
                {
                    CreatedTime = DateTime.Now,
                    Error4xxCount = 0,
                    Error5xxCount = 0,
                    RequestCount = 1,
                });
                break;
        }


        // 获取ip
        var ip = context.Connection.RemoteIpAddress?.ToString();

        // 获取请求头中的IP
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            ip = forwardedFor;
        }
    }
}