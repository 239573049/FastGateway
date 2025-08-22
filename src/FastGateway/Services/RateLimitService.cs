using System.ComponentModel.DataAnnotations;
using AspNetCoreRateLimit;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;

namespace FastGateway.Services;

public static class RateLimitService
{
    public static IServiceCollection AddRateLimitService(this IServiceCollection services, List<RateLimit> rateLimits)
    {
        // 如果启用限流则添加限流中间件
        if (rateLimits.Count != 0)
        {
            services.AddMemoryCache();
            services.Configure<IpRateLimitOptions>
            (options =>
            {
                options.GeneralRules = rateLimits.Select(x => new RateLimitRule
                {
                    Endpoint = x.Endpoint,
                    Period = x.Period,
                    Limit = x.Limit
                }).ToList();

                options.ClientWhitelist.AddRange(rateLimits.SelectMany(x => x.IpWhitelist).ToArray());
                options.DisableRateLimitHeaders = false;
                options.EnableEndpointRateLimiting = true;
                options.EnableRegexRuleMatching = false;
                options.EndpointWhitelist.AddRange(rateLimits.SelectMany(x => x.EndpointWhitelist).ToArray());
                options.IpWhitelist.AddRange(rateLimits.SelectMany(x => x.IpWhitelist).ToArray());
                options.RealIpHeader = "X-Forwarded-For";
                options.RequestBlockedBehaviorAsync = async (context, _, _, _) =>
                {
                    context.Response.StatusCode = 429;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsJsonAsync(ResultDto.CreateFailed("请求过于频繁,请稍后再试"));
                };
            });
            services.AddSingleton<IRateLimitConfiguration,
                RateLimitConfiguration>();
            services.AddInMemoryRateLimiting();
        }

        return services;
    }

    public static IEndpointRouteBuilder UseRateLimitMiddleware(this WebApplication app, List<RateLimit> rateLimits)
    {
        if (rateLimits.Count == 0) return app;

        app.UseIpRateLimiting();

        return app;
    }

    public static IEndpointRouteBuilder MapRateLimit(this IEndpointRouteBuilder app)
    {
        var rateLimit = app.MapGroup("/api/v1/rate-limit")
            .WithTags("限流")
            .WithDescription("限流管理")
            .RequireAuthorization()
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("限流");

        rateLimit.MapPost(string.Empty, (ConfigurationService configService, RateLimit limit) =>
        {
            if (string.IsNullOrWhiteSpace(limit.Name)) throw new ValidationException("限流名称不能为空");

            if (configService.GetRateLimits().Any(x => x.Name == limit.Name)) throw new ValidationException("限流名称已存在");

            configService.AddRateLimit(limit);
        }).WithDescription("创建限流").WithDisplayName("创建限流").WithTags("限流");

        rateLimit.MapGet(string.Empty, (ConfigurationService configService, int page, int pageSize) =>
            {
                var allLimits = configService.GetRateLimits();
                var total = allLimits.Count();
                var result = allLimits
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return new PagingDto<RateLimit>(total, result);
            })
            .WithDescription("获取限流列表")
            .WithDisplayName("获取限流列表")
            .WithTags("限流");

        rateLimit.MapDelete("{id}",
                (ConfigurationService configService, string id) => { configService.DeleteRateLimit(id); })
            .WithDescription("删除限流").WithDisplayName("删除限流").WithTags("限流");

        rateLimit.MapPut("{id}", (ConfigurationService configService, string id, RateLimit rateLimit) =>
        {
            if (string.IsNullOrWhiteSpace(rateLimit.Name)) throw new ValidationException("限流名称不能为空");

            if (configService.GetRateLimits().Any(x => x.Name == rateLimit.Name && x.Id != id))
                throw new ValidationException("限流名称已存在");

            rateLimit.Id = id;
            configService.UpdateRateLimit(rateLimit);
        }).WithDescription("更新限流").WithDisplayName("更新限流").WithTags("限流");

        return app;
    }
}