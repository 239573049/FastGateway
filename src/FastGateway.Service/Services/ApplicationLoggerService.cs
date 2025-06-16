using System.Diagnostics;
using FastGateway.Entities;
using FastGateway.Service.BackgroundTask;
using FastGateway.Service.DataAccess;
using FastGateway.Service.Dto;
using FastGateway.Service.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.Services;

public static class ApplicationLoggerService
{
    public static IEndpointRouteBuilder MapApplicationLogger(this IEndpointRouteBuilder app)
    {
        var applicationLogger = app.MapGroup("/api/v1/applicationLogger")
            .WithTags("应用日志")
            .WithDescription("应用日志管理")
            .AddEndpointFilter<ResultFilter>()
            .RequireAuthorization()
            .WithDisplayName("应用日志");

        applicationLogger.MapGet(string.Empty, async (LoggerContext loggerContext, int page, int pageSize) =>
        {
            var result = await loggerContext.ApplicationLoggers
                .OrderByDescending(x => x.RequestTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var total = await loggerContext.ApplicationLoggers.CountAsync();

            return new PagingDto<ApplicationLogger>(total, result);
        });

        applicationLogger.MapDelete("/deleteOldLogs", async (LoggerContext loggerContext) =>
        {
            var oneMonthAgo = DateTime.Now.AddMonths(-1);
            var logsToDelete = await loggerContext.ApplicationLoggers
                .Where(x => x.RequestTime < oneMonthAgo)
                .ToListAsync();

            if (logsToDelete.Any())
            {
                loggerContext.ApplicationLoggers.RemoveRange(logsToDelete);
                await loggerContext.SaveChangesAsync();
            }

            return new { deletedCount = logsToDelete.Count, message = $"已删除 {logsToDelete.Count} 条一个月前的日志" };
        });

        return app;
    }
}

public class ApplicationLoggerMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // TODO: 暂时不记录GET请求|WebSocket请求
        if (context.Request.Method == "GET" || context.WebSockets.IsWebSocketRequest)
        {
            await next(context);
            return;
        }
        else
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<ApplicationLogger>>();
            var sw = Stopwatch.StartNew();
            var ip = context.Request.Headers["X-Forwarded-For"];
            var applicationLogger = new ApplicationLogger()
            {
                Ip = ip,
                Method = context.Request.Method,
                Path = context.Request.Path,
                Domain = context.Request.Host.Host,
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                RequestTime = DateTime.Now,
            };
            string platform = "Unknown";
            var userAgent = context.Request.Headers.UserAgent.ToString();
            if (userAgent.Contains("Windows"))
            {
                platform = "Windows";
            }
            else if (userAgent.Contains("Linux"))
            {
                platform = "Linux";
            }
            else if (userAgent.Contains("Android") || userAgent.Contains("iPhone") ||
                     userAgent.Contains("iPad"))
            {
                platform = "Mobile";
            }

            try
            {
                await next(context);
            }
            catch (Exception e)
            {
                applicationLogger.Success = false;
                applicationLogger.StatusCode = 500;
                applicationLogger.Platform = platform;
                applicationLogger.Elapsed = sw.ElapsedMilliseconds;
                logger.LogError(e, "网关请求异常");
                LoggerBackgroundTask.AddLogger(applicationLogger);
                return;
            }

            sw.Stop();
            applicationLogger.Platform = platform;
            applicationLogger.Elapsed = sw.ElapsedMilliseconds;
            applicationLogger.Success = context.Response.StatusCode == 200;
            applicationLogger.StatusCode = context.Response.StatusCode;
            LoggerBackgroundTask.AddLogger(applicationLogger);
        }
    }
}