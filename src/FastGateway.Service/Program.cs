using FastGateway.Entities.Core;
using FastGateway.Service.BackgroundTask;
using FastGateway.Service.DataAccess;
using FastGateway.Service.Infrastructure;
using FastGateway.Service.Services;
using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
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
        builder.Services.AddSingleton<ISearcher>(new Searcher(CachePolicy.File, "ip2region.xdb"));
        builder.Services.AddHostedService<LoggerBackgroundTask>();
        builder.Services.AddScoped<SettingProvide>();
        builder.Services.AddDbContext<MasterContext>(optionsBuilder =>
        {
            // 判断当前目录是否存在data文件夹
            if (!Directory.Exists("./data"))
            {
                Directory.CreateDirectory("./data");
            }

            optionsBuilder.UseSqlite("Data Source=./data/gateway.db")
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MasterContext>();
            await dbContext.Database.MigrateAsync();

            var certs = await dbContext.Certs.Where(x => x.RenewStats == RenewStats.Success && x.Expired).ToListAsync();

            CertService.InitCert(certs);

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

        app.Use((async (context, next) =>
        {
            if (context.Request.Path == "/")
            {
                context.Request.Path = "/index.html";
            }

            await next();

            if (context.Response.StatusCode == 404)
            {
                context.Request.Path = "/index.html";
                await next();
            }
        }));
        
        app.UseStaticFiles();

        app.MapDomain()
            .MapBlacklistAndWhitelist()
            .MapCert()
            .MapSetting()
            .MapFileStorage()
            .MapRateLimit()
            .MapApplicationLogger()
            .MapServer();

        await app.RunAsync();
    }
}