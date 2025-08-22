using FastGateway.Services;
using System.Diagnostics;

namespace FastGateway.Middleware;

public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;

    public PerformanceMonitoringMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // 记录请求开始
            QpsService.IncrementServiceRequests();
            
            await _next(context);
            
            // 请求成功完成
            stopwatch.Stop();
            QpsService.RecordSuccessRequest(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception)
        {
            // 请求失败
            stopwatch.Stop();
            QpsService.RecordFailedRequest(stopwatch.ElapsedMilliseconds);
            throw;
        }
    }
}

public static class PerformanceMonitoringMiddlewareExtensions
{
    public static IApplicationBuilder UsePerformanceMonitoring(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PerformanceMonitoringMiddleware>();
    }
}