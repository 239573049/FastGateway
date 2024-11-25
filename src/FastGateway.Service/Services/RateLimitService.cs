using System.ComponentModel.DataAnnotations;
using AspNetCoreRateLimit;
using FastGateway.Entities;
using FastGateway.Service.DataAccess;
using FastGateway.Service.Dto;
using FastGateway.Service.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.Services;

public static class RateLimitService
{
    public static IServiceCollection AddRateLimitService(this IServiceCollection services, RateLimit[] rateLimits)
    {
        // 如果启用限流则添加限流中间件
        if (rateLimits.Length != 0)
        {
            services.AddMemoryCache();
            services.Configure<IpRateLimitOptions>
            (options =>
            {
                options.GeneralRules = rateLimits.Select(x => new RateLimitRule()
                {
                    Endpoint = x.Endpoint,
                    Period = x.Period,
                    Limit = x.Limit,
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

    public static IEndpointRouteBuilder UseRateLimitMiddleware(this WebApplication app, RateLimit[] rateLimits)
    {
        if (rateLimits.Length == 0)
        {
            return app;
        }

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

        rateLimit.MapPost(string.Empty, async (MasterContext dbContext, RateLimit limit) =>
        {
            if (string.IsNullOrWhiteSpace(limit.Name))
            {
                throw new ValidationException("限流名称不能为空");
            }

            if (await dbContext.RateLimits.AnyAsync(x => x.Name == limit.Name))
            {
                throw new ValidationException("限流名称已存在");
            }


            dbContext.RateLimits.Add(limit);

            await dbContext.SaveChangesAsync();
        }).WithDescription("创建限流").WithDisplayName("创建限流").WithTags("限流");

        rateLimit.MapGet(string.Empty, async (MasterContext dbContext, int page, int pageSize) =>
            {
                var total = await dbContext.RateLimits.CountAsync();

                return new PagingDto<RateLimit>(total,  await dbContext.RateLimits
                    .Skip((page - 1) * pageSize)
                    .ToListAsync());
            })
            .WithDescription("获取限流列表")
            .WithDisplayName("获取限流列表")
            .WithTags("限流");

        rateLimit.MapDelete("{id}",
            async (MasterContext dbContext, string id) =>
            {
                await dbContext.RateLimits.Where(x => x.Id == id).ExecuteDeleteAsync();
            }).WithDescription("删除限流").WithDisplayName("删除限流").WithTags("限流");

        rateLimit.MapPut("{id}", async (MasterContext dbContext, string id, RateLimit rateLimit) =>
        {
            if (string.IsNullOrWhiteSpace(rateLimit.Name))
            {
                throw new ValidationException("限流名称不能为空");
            }

            if (await dbContext.RateLimits.AnyAsync(x => x.Name == rateLimit.Name && x.Id != id))
            {
                throw new ValidationException("限流名称已存在");
            }

            rateLimit.Id = id;

            dbContext.RateLimits.Update(rateLimit);

            await dbContext.SaveChangesAsync();
        }).WithDescription("更新限流").WithDisplayName("更新限流").WithTags("限流");

        return app;
    }
}