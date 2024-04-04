namespace FastGateway.Middlewares;

public sealed class StatisticsMiddleware(ICurrentContext currentContext) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.IsHttps)
        {
            // TODO: 由于h3需要对应请求的端口，所以这里需要动态设置
            context.Response.Headers.AltSvc = "h3=\":" + (context.Request.Host.Port ?? 443) + "\"";
        }

        await next(context);

        switch (context.Response.StatusCode)
        {
            case >= 400 and < 500:
                StatisticsBackgroundService.Write(new StatisticRequestCountDto()
                {
                    CreatedTime = DateTime.Now,
                    ServiceId = currentContext.ServiceId,
                    Error4xxCount = 1,
                    RequestCount = 1,
                });
                break;
            case >= 500:
                StatisticsBackgroundService.Write(new StatisticRequestCountDto()
                {
                    CreatedTime = DateTime.Now,
                    ServiceId = currentContext.ServiceId,
                    Error5xxCount = 1,
                    RequestCount = 1,
                });
                break;
            case < 400:
                StatisticsBackgroundService.Write(new StatisticRequestCountDto()
                {
                    CreatedTime = DateTime.Now,
                    ServiceId = currentContext.ServiceId,
                    RequestCount = 1,
                });
                break;
        }

        FastContext.QpsService.IncrementServiceRequests();

        // 获取请求的IP
        var ip = context.Connection.RemoteIpAddress?.ToString();

        // 可能再请求头中
        if (string.IsNullOrWhiteSpace(ip))
        {
            ip = context.Request.Headers["X-Forwarded-For"];
        }

        StatisticsBackgroundService.Write(new StatisticIpDto()
        {
            CreatedTime = DateTime.Now,
            // 如果是本地测试，就随机一个IP
            Ip = ip,
            ServiceId = currentContext.ServiceId,
        });
    }
}