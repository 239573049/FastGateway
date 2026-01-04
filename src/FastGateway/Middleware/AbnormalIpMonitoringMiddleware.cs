using FastGateway.Infrastructure;
using FastGateway.Services;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;

namespace FastGateway.Middleware;

public sealed class AbnormalIpMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _serverId;

    public AbnormalIpMonitoringMiddleware(RequestDelegate next, string serverId)
    {
        _next = next;
        _serverId = serverId;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/internal/gateway", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            RecordIfNeeded(
                context,
                exception: ex,
                errorFeature: context.Features.Get<IForwarderErrorFeature>(),
                statusCode: StatusCodes.Status500InternalServerError);
            throw;
        }

        RecordIfNeeded(
            context,
            exception: null,
            errorFeature: context.Features.Get<IForwarderErrorFeature>(),
            statusCode: context.Response.StatusCode);
    }

    private void RecordIfNeeded(
        HttpContext context,
        Exception? exception,
        IForwarderErrorFeature? errorFeature,
        int statusCode)
    {
        var hasForwarderError = errorFeature != null && errorFeature.Error != ForwarderError.None;
        if (!hasForwarderError && exception == null && statusCode < 500)
        {
            return;
        }

        var ip = ClientIpHelper.GetClientIp(context);
        if (string.IsNullOrWhiteSpace(ip))
        {
            return;
        }

        var description = BuildDescription(exception, errorFeature, statusCode);
        if (string.IsNullOrWhiteSpace(description))
        {
            return;
        }

        AbnormalIpMonitor.Record(
            ip,
            description,
            path: context.Request.Path.Value,
            method: context.Request.Method,
            statusCode: statusCode,
            serverId: _serverId);
    }

    private static string BuildDescription(Exception? exception, IForwarderErrorFeature? errorFeature, int statusCode)
    {
        if (exception != null)
        {
            return $"{exception.GetType().Name}: {exception.Message}";
        }

        if (errorFeature != null && errorFeature.Error != ForwarderError.None)
        {
            var error = errorFeature.Error.ToString();
            var ex = errorFeature.Exception;
            return ex == null ? error : $"{error}: {ex.Message}";
        }

        return $"HTTP {statusCode}";
    }
}

public static class AbnormalIpMonitoringMiddlewareExtensions
{
    public static IApplicationBuilder UseAbnormalIpMonitoring(this IApplicationBuilder app, string serverId)
    {
        return app.UseMiddleware<AbnormalIpMonitoringMiddleware>(serverId);
    }
}
