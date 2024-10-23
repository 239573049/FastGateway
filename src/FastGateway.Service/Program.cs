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
        var builder = WebApplication.CreateBuilder(args);

        var logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateLogger();

        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Name));

        builder.Host.UseSerilog(logger);
        builder.Services.AddHttpClient();

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
        builder.Services.AddResponseCompression();
        builder.Services.AddSingleton<ISearcher>(new Searcher(CachePolicy.File, "ip2region.xdb"));
        builder.Services.AddHostedService<LoggerBackgroundTask>();
        builder.Services.AddHostedService<ClientRequestBackgroundTask>();
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

            var certs = await dbContext.Certs.Where(x => x.Expired == false).ToListAsync();

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