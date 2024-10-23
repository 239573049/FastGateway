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

        dashboard.MapGet(string.Empty, async (MasterContext dbContext) =>
        {
            var dashboardDto = new DashboardDto();

            var now = DateTime.Now.ToString("yyyy-MM-dd");

            dashboardDto.Success = await dbContext.ClientRequestLoggers.SumAsync(x => x.Success);
            dashboardDto.Fail = await dbContext.ClientRequestLoggers.SumAsync(x => x.Fail);
            dashboardDto.Total = dashboardDto.Success + dashboardDto.Fail;
            dashboardDto.TodaySuccess = await dbContext.ClientRequestLoggers.Where(x => x.RequestTime == now)
                .SumAsync(x => x.Success);
            dashboardDto.TodayFail =
                await dbContext.ClientRequestLoggers.Where(x => x.RequestTime == now).SumAsync(x => x.Fail);

            return dashboardDto;
        });

        return app;
    }
}