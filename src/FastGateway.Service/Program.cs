using FastGateway.Service.DataAccess;
using FastGateway.Service.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Serilog.Core;

namespace FastGateway.Service;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        builder.Host.UseSerilog(logger);
        builder.Services.AddHttpClient();
        builder.Services.AddDbContext<MasterContext>(optionsBuilder =>
        {
            optionsBuilder.UseSqlite("Data Source=gateway.db")
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MasterContext>();
            await dbContext.Database.MigrateAsync();

            var server = await dbContext.Servers.ToListAsync();
            var domainNames = await dbContext.DomainNames.ToListAsync();
            var blacklistAndWhitelists = await dbContext.BlacklistAndWhitelists.ToListAsync();
            var rateLimits = await dbContext.RateLimits.ToListAsync();
            foreach (var item in server)
            {
                await Task.Factory.StartNew(async () =>
                    await Gateway.Gateway.BuilderGateway(item, domainNames.Where(x => x.ServerId == item.Id).ToList(),
                        blacklistAndWhitelists, rateLimits));
            }
        }

        app.MapDomain()
            .UseBlacklistAndWhitelist()
            .MapCert()
            .MapRateLimit()
            .MapServer();

        await app.RunAsync();
    }
}