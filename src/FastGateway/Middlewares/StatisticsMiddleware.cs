using FastGateway.BackgroundServices;

namespace FastGateway.Middlewares;

public sealed class StatisticsMiddleware(ICurrentContext currentContext) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        await next(context);

        switch (context.Response.StatusCode)
        {
            case >= 400 and < 500:
                StatisticsBackgroundService.WriteAsync(new StatisticRequestCountDto()
                {
                    CreatedTime = DateTime.Now,
                    ServiceId = currentContext.ServiceId,
                    Error4xxCount = 1,
                    RequestCount = 1,
                });
                break;
            case >= 500:
                StatisticsBackgroundService.WriteAsync(new StatisticRequestCountDto()
                {
                    CreatedTime = DateTime.Now,
                    ServiceId = currentContext.ServiceId,
                    Error5xxCount = 1,
                    RequestCount = 1,
                });
                break;
            case < 400:
                StatisticsBackgroundService.WriteAsync(new StatisticRequestCountDto()
                {
                    CreatedTime = DateTime.Now,
                    ServiceId = currentContext.ServiceId,
                    RequestCount = 1,
                });
                break;
        }

        FastContext.QpsService.IncrementServiceRequests(currentContext.ServiceId);
    }
}