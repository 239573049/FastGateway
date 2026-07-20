using FastGateway.Infrastructure;

namespace FastGateway.Services.Statistics;

public static class StatisticsService
{
    public static IEndpointRouteBuilder MapStatistics(this IEndpointRouteBuilder app)
    {
        // 查询侧懒初始化（后台服务未跑起来之前也能安全响应）
        StatisticsDb.Initialize();

        var statistics = app.MapGroup("/api/v1/statistics")
            .WithTags("统计分析")
            .WithDescription("流量统计分析")
            .RequireAuthorization()
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("统计分析");

        statistics.MapGet("overview", (string? range, string? host) =>
                StatisticsQueryService.GetOverview(range, host))
            .WithDescription("统计概览")
            .WithDisplayName("统计概览");

        statistics.MapGet("timeseries", (string? range, string? host) =>
                StatisticsQueryService.GetTimeSeries(range, host))
            .WithDescription("时间序列")
            .WithDisplayName("时间序列");

        statistics.MapGet("geo", (string? range, string? host, string? scope, string? mode, int? top) =>
                StatisticsQueryService.GetGeo(range, host, scope, mode, top ?? 20))
            .WithDescription("地理分布")
            .WithDisplayName("地理分布");

        statistics.MapGet("rankings",
                (string? range, string? host, string? type, string? mode, int? top, int? page, int? pageSize) =>
                StatisticsQueryService.GetRankings(range, host, type, mode, top ?? 10, page, pageSize))
            .WithDescription("维度排行")
            .WithDisplayName("维度排行");

        statistics.MapGet("requests",
                (string? range, string? host, string? ip, int? status, bool? blocked, int? page, int? pageSize) =>
                StatisticsQueryService.GetRequests(range, host, ip, status, blocked, page ?? 1, pageSize ?? 20))
            .WithDescription("请求明细")
            .WithDisplayName("请求明细");

        return app;
    }
}
