using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Security;
using FastGateway.Services;
using FastGateway.Services.Statistics;

namespace FastGateway.Middleware;

public sealed class AdaptiveThreatProtectionMiddleware(
    RequestDelegate next,
    ILogger<AdaptiveThreatProtectionMiddleware> logger,
    string serverId)
{
    private const string ProfileKey = "FastGateway.AdaptiveThreatProfile";

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "/";
        if (AdaptiveThreatDetector.IsIgnoredPath(path))
        {
            await next(context);
            return;
        }

        var ip = ClientIpHelper.GetClientIp(context);
        if (!AdaptiveThreatDetector.ShouldTrackIp(ip) || BlacklistAndWhitelistService.IsWhitelisted(ip))
        {
            await next(context);
            return;
        }

        var userAgent = context.Request.Headers.UserAgent.ToString();
        var profile = AdaptiveThreatDetector.InspectRequest(ip!, path, context.Request.Method, userAgent);
        if (profile.BlockNow)
        {
            context.Items[StatisticsCollector.BlockReasonKey] = (byte)BlockReason.AdaptiveThreat;
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers.RetryAfter = Math.Max(1, profile.RetryAfterSeconds).ToString();
            context.Response.Headers["X-Security-Policy"] = "adaptive-threat";
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(
                ResultDto.CreateFailed("请求已被自适应安全策略拦截"),
                AppJsonContext.Default.ResultDto);
            return;
        }

        context.Items[ProfileKey] = profile;
        await next(context);

        if (context.Items[ProfileKey] is not AdaptiveRequestProfile observed) return;
        var quarantine = AdaptiveThreatDetector.ObserveResponse(observed, context.Response.StatusCode, serverId);
        if (quarantine == null) return;

        logger.LogWarning(
            "自适应威胁检测已隔离 IP {Ip}，评分 {Score}，原因 {Reason}，最近路径 {Path}",
            quarantine.Ip,
            quarantine.Score,
            quarantine.Reason,
            quarantine.LastPath);
    }
}

public static class AdaptiveThreatProtectionMiddlewareExtensions
{
    public static IApplicationBuilder UseAdaptiveThreatProtection(this IApplicationBuilder app, string serverId)
    {
        return app.UseMiddleware<AdaptiveThreatProtectionMiddleware>(serverId);
    }
}
