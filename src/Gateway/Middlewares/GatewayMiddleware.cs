namespace Gateway.Middlewares;

public class GatewayMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // TODO: 由于h3需要对应请求的端口，所以这里需要动态设置
        context.Response.Headers.AltSvc = "h3=\":" + context.Request.Host.Port + "\"";
        await next(context);
    }
}