using FastGateway.Dto;
using FastGateway.Infrastructure;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace FastGateway.Middleware;

public sealed class ProxyErrorResponseMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _gatewayVersion;

    public ProxyErrorResponseMiddleware(RequestDelegate next, string gatewayVersion)
    {
        _next = next;
        _gatewayVersion = gatewayVersion;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        if (context.Response.HasStarted)
        {
            return;
        }

        if (context.Request.Path.StartsWithSegments("/internal/gateway", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var statusCode = context.Response.StatusCode;
        if (statusCode is not (StatusCodes.Status502BadGateway or StatusCodes.Status503ServiceUnavailable or StatusCodes.Status504GatewayTimeout))
        {
            return;
        }

        var errorFeature = context.Features.Get<IForwarderErrorFeature>();
        var error = errorFeature?.Error ?? ForwarderError.None;
        var exception = errorFeature?.Exception;

        var payload = new ProxyErrorDto
        {
            Code = statusCode,
            Message = GetMessage(statusCode, error),
            Error = error == ForwarderError.None ? GetFallbackError(statusCode) : error.ToString(),
            Detail = exception?.Message,
            RequestId = context.TraceIdentifier,
            Timestamp = DateTimeOffset.UtcNow,
            Path = context.Request.Path.Value
        };

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers["Server"] = "FastGateway";
        context.Response.Headers["X-FastGateway-Version"] = _gatewayVersion;
        await context.Response.WriteAsJsonAsync(payload, AppJsonContext.Default.ProxyErrorDto);
    }

    private static string GetMessage(int statusCode, ForwarderError error)
    {
        return statusCode switch
        {
            StatusCodes.Status503ServiceUnavailable when error == ForwarderError.NoAvailableDestinations
                => "当前没有可用的上游服务节点",
            StatusCodes.Status503ServiceUnavailable
                => "上游服务暂时不可用",
            StatusCodes.Status504GatewayTimeout when error == ForwarderError.RequestTimedOut
                => "上游服务响应超时",
            StatusCodes.Status504GatewayTimeout
                => "网关转发请求超时",
            StatusCodes.Status502BadGateway
                => "网关转发请求失败",
            _ => "代理请求失败"
        };
    }

    private static string GetFallbackError(int statusCode)
    {
        return statusCode switch
        {
            StatusCodes.Status503ServiceUnavailable => "ServiceUnavailable",
            StatusCodes.Status504GatewayTimeout => "GatewayTimeout",
            StatusCodes.Status502BadGateway => "BadGateway",
            _ => "ProxyError"
        };
    }
}

public static class ProxyErrorResponseMiddlewareExtensions
{
    public static IApplicationBuilder UseProxyErrorResponse(this IApplicationBuilder app, string gatewayVersion)
    {
        return app.UseMiddleware<ProxyErrorResponseMiddleware>(gatewayVersion);
    }
}
