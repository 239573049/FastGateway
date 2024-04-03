namespace FastGateway.Middlewares;

public sealed class StatisticsMiddleware(ICurrentContext currentContext) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
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

        var ips = new string[]
        {
            "121.35.0.122",
            "117.147.0.196",
            "146.19.24.28",
            "203.135.98.155",
            "129.153.125.162"
        };

        StatisticsBackgroundService.Write(new StatisticIpDto()
        {
            CreatedTime = DateTime.Now,
            // 如果是本地测试，就随机一个IP
            Ip = ips[new Random().Next(0, ips.Length)],
            ServiceId = currentContext.ServiceId,
        });
    }
}