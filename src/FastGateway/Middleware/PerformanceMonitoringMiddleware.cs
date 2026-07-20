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
        // /api/v1/qps 内部含 1 秒阻塞的网络采样，且被仪表盘每 3 秒轮询，
        // 计入统计会把 P95/P99 与成功率数据污染成“监控自己”，这里直接跳过。
        if (context.Request.Path.StartsWithSegments("/api/v1/qps", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

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