using System.Runtime.InteropServices;
using System.Text;
using FastGateway.Service.BackgroundTask;
using FastGateway.Service.Infrastructure;
using FastGateway.Service.Options;
using FastGateway.Service.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Watermelon.Service.Infrastructure;

namespace FastGateway.Service;

public static class Program
{
    public static async Task Main(string[] args)
    {
        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = args
        });

        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Name));

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

        builder.Services.AddTransient<JwtHelper>();
        builder.Services.AddScoped<SettingProvide>();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new DateTimeJsonConverter());
        });
        builder.Services.AddSystemUsage();
        builder.Services.AddResponseCompression();
        
        builder.Services.AddHostedService<RenewSSLBackgroundService>();
        builder.Services.AddSingleton<ConfigurationService>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var configService = scope.ServiceProvider.GetRequiredService<ConfigurationService>();

            var certs = configService.GetActiveCerts();
            CertService.InitCert(certs);

            var domainNames = configService.GetDomainNames();
            var blacklistAndWhitelists = configService.GetBlacklistAndWhitelists();
            var rateLimits = configService.GetRateLimits();
            
            foreach (var item in configService.GetServers())
            {
                await Task.Factory.StartNew(async () =>
                    await Gateway.Gateway.BuilderGateway(item, configService.GetDomainNamesByServerId(item.Id),
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
            .MapServer();

        await app.RunAsync();
    }
}