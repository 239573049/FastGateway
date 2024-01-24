namespace Gateway.Middlewares;

public class GatewayMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.Headers.AltSvc = "h3=\":" + context.Request.Host.Port + "\"";
        await next(context);
    }
}