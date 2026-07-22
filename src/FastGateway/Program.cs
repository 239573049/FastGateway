using System.Runtime.InteropServices;
using System.Text;
using FastGateway.BackgroundTask;
using FastGateway.Infrastructure;
using FastGateway.Middleware;
using FastGateway.Options;
using FastGateway.Services;
using FastGateway.Services.Statistics;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace FastGateway;

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

        FastGatewayOptions.Initialize(builder.Configuration);

        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Name));

        builder.Services.AddHttpClient();

        // 判断是否window，
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            builder.Services.AddWindowsService(option => { option.ServiceName = "FastGateway"; });

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.Name).Get<JwtOptions>();

        builder.Services
            .AddAuthorization()
            .AddAuthentication(options => { options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme; })
            .AddJwtBearer(options =>
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
            });

        builder.Services.AddTransient<JwtHelper>();
        builder.Services.AddScoped<SettingProvide>();

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new DateTimeJsonConverter());
        });
        builder.Services.AddSystemUsage();
        builder.Services.AddResponseCompression();

        // 宿主级总保险：后台服务（统计、证书续期等）抛出未捕获异常时，默认行为 StopHost 会
        // 连带停掉整个网关，导致所有客户端连接被断开。改为 Ignore，任何后台任务崩溃都不再拖垮
        // 转发主流程；后台服务内部各自记录日志并自愈。
        builder.Services.Configure<HostOptions>(options =>
            options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);

        builder.Services.AddHostedService<RenewSslBackgroundService>();
        builder.Services.AddHostedService<StatisticsBackgroundService>();
        builder.Services.AddSingleton<ConfigurationService>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var configService = scope.ServiceProvider.GetRequiredService<ConfigurationService>();

            var certs = configService.GetActiveCerts();
            CertService.InitCert(certs);

            var blacklistAndWhitelists = configService.GetBlacklistAndWhitelists();
            var rateLimits = configService.GetRateLimits();

            BlacklistAndWhitelistService.RefreshCache(blacklistAndWhitelists);

            foreach (var item in configService.GetServers())
                await Task.Factory.StartNew(async () =>
                    await Gateway.Gateway.BuilderGateway(item, configService.GetDomainNamesByServerId(item.Id),
                        blacklistAndWhitelists, rateLimits));

            // 启动 L4 端口转发（TCP/UDP）
            foreach (var item in configService.GetStreamForwards())
                _ = Gateway.StreamProxyManager.StartAsync(item);
        }

        app.Use(async (context, next) =>
        {
            await next();

            if (context.Response.StatusCode == 404)
            {
                context.Request.Path = "/index.html";
                await next();
            }
        });

        app.UseResponseCompression();
        
        app.UsePerformanceMonitoring();

        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapDomain()
            .MapBlacklistAndWhitelist()
            .MapAbnormalIp()
            .MapCert()
            .MapSetting()
            .MapApiQpsService()
            .MapFileStorage()
            .MapRateLimit()
            .MapAuthorizationService()
            .MapServer()
            .MapStreamForward()
            .MapTunnel()
            .MapSystem()
            .MapStatistics();

        await app.RunAsync();
    }
}
