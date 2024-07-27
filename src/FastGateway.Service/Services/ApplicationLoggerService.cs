using FastGateway.Entities;
using FastGateway.Service.DataAccess;
using FastGateway.Service.Dto;
using FastGateway.Service.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FastGateway.Service.Services;

public static class ApplicationLoggerService
{
    public static WebApplication MapApplicationLogger(this WebApplication app)
    {
        var applicationLogger = app.MapGroup("/api/v1/applicationLogger")
            .WithTags("应用日志")
            .WithDescription("应用日志管理")
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("应用日志");

        applicationLogger.MapGet(string.Empty, async (MasterContext dbContext, int page, int pageSize) =>
        {
            var result = await dbContext.ApplicationLoggers
                .OrderByDescending(x => x.RequestTime)
                .Skip((page - 1) * pageSize)
                .ToListAsync();

            var total = await dbContext.ApplicationLoggers.CountAsync();

            return new PagingDto<ApplicationLogger>(total, result);
        });

        return app;
    }
}