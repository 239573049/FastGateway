using System.Text.Json;
using FastGateway.Service.DataAccess;
using FastGateway.Service.Dto;
using FastGateway.Service.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.Services;

public static class DashboardService
{
    public static IEndpointRouteBuilder MapDashboard(this IEndpointRouteBuilder app)
    {
        var dashboard = app.MapGroup("/api/v1/dashboard")
            .WithTags("仪表盘")
            .RequireAuthorization()
            .WithDescription("仪表盘")
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("仪表盘");

        dashboard.MapGet(string.Empty, async (LoggerContext loggerContext) =>
        {
            var dashboardDto = new DashboardDto();

            var now = DateTime.Now.ToString("yyyy-MM-dd");

            dashboardDto.Success = await loggerContext.ClientRequestLoggers.SumAsync(x => x.Success);
            dashboardDto.Fail = await loggerContext.ClientRequestLoggers.SumAsync(x => x.Fail);
            dashboardDto.Total = dashboardDto.Success + dashboardDto.Fail;
            dashboardDto.TodaySuccess = await loggerContext.ClientRequestLoggers.Where(x => x.RequestTime == now)
                .SumAsync(x => x.Success);
            dashboardDto.TodayFail =
                await loggerContext.ClientRequestLoggers.Where(x => x.RequestTime == now).SumAsync(x => x.Fail);

            return dashboardDto;
        });

        // 实时数据
        dashboard.MapGet("/realtime", async (ISystemUsage systemUsage, HttpContext context) =>
        {
            for (int i = 0; i < 10; i++)
            {
                // 获取系统使用率

                var cpu = systemUsage.GetCpuUsage();
                var memory = systemUsage.GetMemoryUsage();
                var disk = systemUsage.GetDiskUsage();


                await context.Response.WriteAsync($"data:{JsonSerializer.Serialize(new
                {
                    cpu,
                    memory.memoryUsage,
                    memory.totalMemory,
                    memory.useMemory,
                    diskRead = disk.read,
                    diskWrite = disk.write
                })}\n\n");

                await context.Response.Body.FlushAsync();

                await Task.Delay(1000);
            }
        });

        return app;
    }
}