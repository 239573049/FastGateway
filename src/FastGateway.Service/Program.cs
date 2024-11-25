using System.Runtime.InteropServices;
using System.Text;
using FastGateway.Service.BackgroundTask;
using FastGateway.Service.DataAccess;
using FastGateway.Service.Infrastructure;
using FastGateway.Service.Options;
using FastGateway.Service.Services;
using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Watermelon.Service.Infrastructure;

namespace FastGateway.Service;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = args
        });

        var logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Name));

        builder.Host.UseSerilog(logger);
        builder.Services.AddHttpClient();

        // 判断是否window，
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            builder.Services.AddWindowsService(option =>
            {
                option.ServiceName = "FastGateway";
            });
        }

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.Name).Get<JwtOptions>();

        builder.Services
            .AddAuthorization()
            .AddAuthentication((options => { options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme; }))
            .AddJwtBearer((options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ClockSkew = TimeSpan.FromDays(jwtOptions.ExpireDay),
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret))
                };
            }));

        builder.Services.AddAutoGnarly();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new DateTimeJsonConverter());
        });
        builder.Services.AddSystemUsage();
        
        builder.Services.AddResponseCompression();
        builder.Services.AddSingleton<ISearcher>(new Searcher(CachePolicy.File, "ip2region.xdb"));
        builder.Services.AddHostedService<LoggerBackgroundTask>();
        builder.Services.AddHostedService<RenewSSLBackgroundService>();
        builder.Services.AddHostedService<ClientRequestBackgroundTask>();
        builder.Services.AddHostedService<LogCleaningBackgroundService>();
        builder.Services.AddDbContext<MasterContext>(optionsBuilder =>
        {
            // 判断当前目录是否存在data文件夹
            if (!Directory.Exists("./data"))
            {
                Directory.CreateDirectory("./data");
            }

            optionsBuilder.UseSqlite(builder.Configuration["Master"])
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        builder.Services.AddDbContext<LoggerContext>(optionsBuilder =>
        {
            // 判断当前目录是否存在data文件夹
            if (!Directory.Exists("./data"))
            {
                Directory.CreateDirectory("./data");
            }

            optionsBuilder.UseSqlite(builder.Configuration["Logger"])
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
        });

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var loggerContext = scope.ServiceProvider.GetRequiredService<LoggerContext>();
            var dbContext = scope.ServiceProvider.GetRequiredService<MasterContext>();
            await dbContext.Database.MigrateAsync();
			await loggerContext.Database.MigrateAsync();

			var certs = await dbContext.Certs.Where(x => x.Expired == false).ToArrayAsync();

            CertService.InitCert(certs);

            var domainNames = await dbContext.DomainNames.ToArrayAsync();
            var blacklistAndWhitelists = await dbContext.BlacklistAndWhitelists.ToListAsync();
            var rateLimits = await dbContext.RateLimits.ToListAsync();
            foreach (var item in  await dbContext.Servers.ToArrayAsync())
            {
                await Task.Factory.StartNew(async () =>
                    await Gateway.Gateway.BuilderGateway(item, domainNames.Where(x => x.ServerId == item.Id).ToArray(),
                        blacklistAndWhitelists, rateLimits));
            }
        }

        app.Use((async (context, next) =>
        {
            await next();

            if (context.Response.StatusCode == 404)
            {
                context.Request.Path = "/index.html";
                await next();
            }
        }));

        app.UseResponseCompression();

        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapDomain()
            .MapBlacklistAndWhitelist()
            .MapCert()
            .MapSetting()
            .MapApiQpsService()
            .MapFileStorage()
            .MapRateLimit()
            .MapAuthorizationService()
            .MapDashboard()
            .MapApplicationLogger()
            .MapServer();

        await app.RunAsync();
    }
}