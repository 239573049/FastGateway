using System.Runtime.InteropServices;
using System.Text;
using FastGateway.BackgroundTask;
using FastGateway.Cluster;
using FastGateway.Infrastructure;
using FastGateway.Middleware;
using FastGateway.Options;
using FastGateway.Risk;
using FastGateway.Risk.Model;
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

        // 注意：不要开启 Http2UnencryptedSupport 全局开关。开启后 Version=2.0 + OrLower 的
        // 明文请求会先发 h2c prior-knowledge、失败再回退，高并发下建连翻倍导致 ENFILE。
        // 隧道路径的明文 HTTP/2 由 RequestVersionExact 显式启用（.NET 5+ 原生支持，无需开关）。

        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ContentRootPath = AppContext.BaseDirectory,
            Args = args
        });

        FastGatewayOptions.Initialize(builder.Configuration);

        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Name));
        builder.Services.AddBotProtection(builder.Configuration);

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
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<AdminLoginGuard>();
        // 风控模型只在主宿主评估；失败已体现在模型状态中，去掉默认请求日志，避免每次提供商故障都刷异常堆栈
        builder.Services.AddHttpClient(JevClient.HttpClientName).RemoveAllLoggers();
        builder.Services.AddSingleton<JevClient>();
        builder.Services.AddSingleton<RiskModelScorerService>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<RiskModelScorerService>());

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default);
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
        builder.Services.AddSingleton<ClusterStateService>();
        builder.Services.AddHostedService<ClusterNodeAgentService>();
        builder.Services.AddHostedService<ClusterRelayAgentService>();

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var configService = scope.ServiceProvider.GetRequiredService<ConfigurationService>();

            // 隧道节点持久化桥接：网关子应用与管理 API 统一经主实例读写节点数据
            Tunnels.TunnelNodeStore.Initialize(configService);

            // 集群中枢：订阅配置变更，主网关角色下自动向从节点推送
            ClusterHub.Initialize(configService, scope.ServiceProvider.GetRequiredService<ClusterStateService>());

            // 登录风控签名密钥：网关子应用启动前就绪，首次启动自动生成
            RiskSigningKey.Initialize(configService, scope.ServiceProvider.GetRequiredService<ClusterStateService>());
            RiskSettingsStore.Initialize(configService);
            RiskModelProviderStore.Initialize(configService);

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

        // 集群同步通道使用 WebSocket
        app.UseWebSockets();

        // 集群数据面隧道：从节点出站注册，供跨节点请求中继（NodeToken 鉴权）
        app.Map(ClusterTunnelHub.EndpointPath, tunnel => tunnel.Run(ClusterTunnelHub.HandleAsync));

        StatisticsDb.Initialize(app.Services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("StatisticsDb"));

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
            .MapCluster()
            .MapStatistics()
            .MapRisk();

        await app.RunAsync();
    }
}
