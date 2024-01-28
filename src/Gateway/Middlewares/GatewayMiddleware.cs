namespace Gateway.Middlewares;

public class GatewayMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // TODO: 由于h3需要对应请求的端口，所以这里需要动态设置
        context.Response.Headers.AltSvc = "h3=\":" + context.Request.Host.Port + "\"";

        // 添加X-Forwarded-For头，用于获取真实的客户端IP
        var ip = context.Connection.RemoteIpAddress?.MapToIPv4()?.ToString();

        context.Request.Headers["X-Forwarded-For"] = ip;

        context.Response.Headers["Gateway-Version"] = GatewayOptions.Version;

        await next(context);
    }
}