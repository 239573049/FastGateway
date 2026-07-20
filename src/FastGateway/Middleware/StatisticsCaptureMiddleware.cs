using System.Diagnostics;
using FastGateway.Infrastructure;
using FastGateway.Services.Statistics;

namespace FastGateway.Middleware;

/// <summary>
///     请求统计采集中间件：仅填充结构体并写入有界通道，geo/UA 解析与持久化全部在后台完成。
/// </summary>
public sealed class StatisticsCaptureMiddleware
{
    private static readonly string[] PageExtensions = [".html", ".htm", ".php", ".asp", ".aspx", ".jsp"];

    private readonly RequestDelegate _next;
    private readonly string _serverId;

    public StatisticsCaptureMiddleware(RequestDelegate next, string serverId)
    {
        _next = next;
        _serverId = serverId;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path;
        if (path.StartsWithSegments("/internal/gateway", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWithSegments("/.well-known/acme-challenge", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        finally
        {
            Record(context, start);
        }
    }

    private void Record(HttpContext context, long start)
    {
        try
        {
            var elapsedMs = (int)Math.Min(Stopwatch.GetElapsedTime(start).TotalMilliseconds, int.MaxValue);
            var status = context.Response.StatusCode;
            var blocked = context.Items.TryGetValue(StatisticsCollector.BlockReasonKey, out var reason)
                ? (byte)(reason is byte b ? b : 0)
                : (byte)0;

            var request = context.Request;
            StatisticsCollector.Enqueue(new RequestStatEntry
            {
                Ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ServerId = _serverId,
                Host = request.Host.Host.ToLowerInvariant(),
                Path = request.Path.Value ?? "/",
                Method = request.Method,
                Status = status,
                ElapsedMs = elapsedMs,
                Ip = ClientIpHelper.GetClientIp(context),
                UserAgent = request.Headers.UserAgent.Count > 0 ? request.Headers.UserAgent.ToString() : null,
                Referer = request.Headers.Referer.Count > 0 ? request.Headers.Referer.ToString() : null,
                Blocked = blocked,
                IsPage = IsPageView(request, status)
            });
        }
        catch
        {
            // 统计采集绝不影响请求处理
        }
    }

    /// <summary>
    ///     PV 判定：GET 且非错误响应，优先 Sec-Fetch-Dest: document，否则按扩展名启发式。
    /// </summary>
    private static bool IsPageView(HttpRequest request, int status)
    {
        if (!HttpMethods.IsGet(request.Method) || status >= 400) return false;

        if (request.Headers.TryGetValue("Sec-Fetch-Dest", out var dest))
            return string.Equals(dest.ToString(), "document", StringComparison.OrdinalIgnoreCase);

        var path = request.Path.Value;
        if (string.IsNullOrEmpty(path)) return true;

        var lastDot = path.LastIndexOf('.');
        var lastSlash = path.LastIndexOf('/');
        if (lastDot < 0 || lastDot < lastSlash) return true;

        var extension = path.AsSpan(lastDot);
        foreach (var pageExtension in PageExtensions)
            if (extension.Equals(pageExtension, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}

public static class StatisticsCaptureMiddlewareExtensions
{
    public static IApplicationBuilder UseStatisticsCapture(this IApplicationBuilder app, string serverId)
    {
        return app.UseMiddleware<StatisticsCaptureMiddleware>(serverId);
    }
}
