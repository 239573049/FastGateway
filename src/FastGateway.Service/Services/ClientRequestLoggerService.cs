using FastGateway.Service.BackgroundTask;
using FastGateway.Service.Dto;

namespace FastGateway.Service.Services;

public class ClientRequestLoggerMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var ip = context.Request.Headers["X-Forwarded-For"];

        await next(context);

        var success = context.Response.StatusCode is >= 200 and < 300;

        ClientRequestBackgroundTask.AddLogger(new ClientRequestLoggerInput(ip.ToString(), success));
    }
}