using FastGateway.Middlewares.FlowAnalytics;
using Microsoft.AspNetCore.Mvc;

namespace FastGateway.Services;

public sealed class PortManagementService(
    IFreeSql freeSql,
    IFlowAnalyzer flowAnalyzer,
    RequestSourceService requestSourceService)
{
    private static readonly Dictionary<string, WebApplication> WebApplications = new();
    private static readonly Dictionary<string, InMemoryConfigProvider> InMemoryConfigProviders = new();

    public async Task Loading()
    {
        foreach (var entity in await freeSql.Select<PortManagementEntity>().Where(x => x.Enable).ToListAsync())
        {
            WebApplications.Add(entity.Id, await RunWebApi(entity));
        }
    }

    private async Task<WebApplication> RunWebApi(PortManagementEntity entity)
    {
        var builder = WebApplication.CreateBuilder();

        var directory = new DirectoryInfo("/data");
        if (!directory.Exists) directory.Create();

        builder.WebHost.UseKestrel(options =>
        {
            if (entity.IsHttps)
            {
                // 配置多个域名证书
                options.ConfigureHttpsDefaults(adapterOptions =>
                {
                    // 获取环境变量
                    var https_password = Environment.GetEnvironmentVariable("HTTPS_PASSWORD") ?? "BaseOS2023";
                    var https_file = Environment.GetEnvironmentVariable("HTTPS_FILE") ?? "gateway.pfx";

                    adapterOptions.ServerCertificateSelector = (_, name) =>
                    {
                        // 从Certificate服务中获取
                        if (string.IsNullOrEmpty(name) ||
                            !CertificateService.CertificateEntityDict.TryGetValue(name, out var certificate))
                            // 创建一个默认的证书
                            return new X509Certificate2(
                                Path.Combine(AppContext.BaseDirectory, "certificates", https_file),
                                https_password);

                        var path = Path.Combine("/data/", certificate.Path);

                        if (File.Exists(path)) return new X509Certificate2(path, certificate.Password);

                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"证书文件不存在：{path}");
                        Console.ResetColor();

                        return new X509Certificate2(Path.Combine(AppContext.BaseDirectory, "certificates", https_file),
                            https_password);
                    };
                });
            }
        });

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Limits.MaxRequestBodySize = entity.MaxRequestBodySize;

            kestrel.ListenAnyIP(entity.Port, portOptions =>
            {
                // TODO: HTTPS3是强制开启HTTPS的
                if (entity.IsHttps)
                {
                    portOptions.Protocols =
                        entity.EnableHttp3 ? HttpProtocols.Http1AndHttp2AndHttp3 : HttpProtocols.Http1AndHttp2;
                    portOptions.UseHttps();
                }
                else
                {
                    portOptions.Protocols = HttpProtocols.Http1;
                }

                if (entity.EnableFlowMonitoring) portOptions.Use<FlowAnalyzeMiddleware>();
            });
        });

        #region Jwt

        var policies = await freeSql.Select<RouteEntity>().Where(a => a.AuthorizationPolicy != null).Distinct()
            .ToListAsync();

        builder.Services
            .AddAuthorization(options =>
            {
                // 添加授权策略
                foreach (var policy in policies)
                    options.AddPolicy(policy.AuthorizationPolicy!, policy => policy.RequireAuthenticatedUser());
            })
            .AddJwtBearerAuthentication(policies);

        #endregion

        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = entity.MaxRequestBodySize;
        });

        builder.Services.Configure<FormOptions>(x =>
        {
            x.ValueLengthLimit = int.MaxValue;
            x.MultipartBodyLengthLimit = int.MaxValue; // if don't set default value is: 128 MB
            x.MultipartHeadersLengthLimit = int.MaxValue;
        });

        builder.Services.AddHostedService<FlowBackgroundService>();

        if (entity.EnableRequestSource)
        {
            builder.Services.AddHostedService<RequestSourceBackgroundService>();
        }

        // 获取环境变量是否启用离线IP归属地
        if (entity.EnableOfflineHomeAddress == false)
        {
            builder.Services.AddOnLineHomeAddress();
        }
        else
        {
            builder.Services.AddOfflineHomeAddress();
        }

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                builder => builder
                    .SetIsOriginAllowed(_ => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
        });

        builder.Services.AddSingleton<StaticFileProxyMiddleware>();

        builder.Services.AddSingleton<GatewayMiddleware>();

        if (entity.EnableRequestSource)
        {
            builder.Services.AddSingleton<RequestSourceService>(requestSourceService);
            builder.Services.AddSingleton<RequestSourceMiddleware>();
        }

        builder.Services.AddSingleton<IContentTypeProvider, FileExtensionContentTypeProvider>();

        builder.Services.AddSingleton(freeSql);

        // 使用内存加载配置 
        builder.Services.AddReverseProxy()
            // .LoadFromMemory(GatewayService.Routes, GatewayService.Clusters)
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

        #region InMemoryConfigProvider

        var inMemoryConfigProvider = new InMemoryConfigProvider(GatewayService.Routes, GatewayService.Clusters);
        InMemoryConfigProviders.Add(entity.Id, inMemoryConfigProvider);
        builder.Services.AddSingleton(inMemoryConfigProvider);
        builder.Services.AddSingleton(s => (IProxyConfigProvider)s.GetRequiredService<InMemoryConfigProvider>());

        #endregion

        if (entity.EnableTunnel)
        {
            builder.Services.AddTunnelServices();
        }

        builder.Services.AddSingleton<IFlowAnalyzer>(flowAnalyzer);

        var app = builder.Build();

        app.UseCors("AllowAll");

        app.UseMiddleware<GatewayMiddleware>();
        app.UseMiddleware<StaticFileProxyMiddleware>();

        if (entity.EnableRequestSource)
        {
            app.UseMiddleware<RequestSourceMiddleware>();
        }

        // 添加自定义授权
        app.UseCustomAuthentication();


        if (entity.EnableTunnel)
        {
            app.MapWebSocketTunnel("/api/gateway/connect-ws");

            // Auth可以添加到这个端点，我们可以将它限制在某些点上
            // 避免外部流量撞击它
            app.MapHttp2Tunnel("/api/gateway/connect-h2");
        }

        app.MapReverseProxy();

        await Task.Run(async () =>
        {
            try
            {
                app.RunAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                entity.Enable = false;

                await FreeSqlContext.FreeSql.Update<PortManagementEntity>()
                    .Where(x => x.Id == entity.Id)
                    .Set(x => x.Enable, false)
                    .ExecuteAffrowsAsync();
            }
        });

        return app;
    }

    public static async Task<PageResultDto<PortManagementEntity>> GetListAsync(int page, int pageSize)
    {
        var result = await FreeSqlContext.FreeSql.Select<PortManagementEntity>()
            .Count(out var total)
            .Page(page, pageSize)
            .ToListAsync();

        return new PageResultDto<PortManagementEntity>(total: total, items: result);
    }

    public async Task<PortManagementEntity> AddAsync(PortManagementEntity entity)
    {
        // 判断端口是否被占用
        if (await FreeSqlContext.FreeSql.Select<PortManagementEntity>().AnyAsync(x => x.Port == entity.Port))
            throw new Exception("端口已被占用");

        if (await FreeSqlContext.FreeSql.Select<PortManagementEntity>().AnyAsync(x => x.Name == entity.Name))
            throw new Exception("名称已被占用");

        if (entity is { EnableHttp3: true, IsHttps: false })
            throw new Exception("HTTP3必须使用HTTPS");

        entity.Id = Guid.NewGuid().ToString("N");
        entity.CreatedTime = DateTime.Now;
        await FreeSqlContext.FreeSql.Insert(entity).ExecuteAffrowsAsync();

        if (entity.Enable)
        {
            WebApplications.Add(entity.Id, await RunWebApi(entity));
        }

        return entity;
    }

    /// <summary>
    /// 启动或停止
    /// </summary>
    /// <param name="id"></param>
    public async Task EnableAsync(string id)
    {
        if (WebApplications.TryGetValue(id, out var webApplication))
        {
            await webApplication.StopAsync();
            InMemoryConfigProviders.Remove(id);
        }
        else
        {
            var entity = await FreeSqlContext.FreeSql.Select<PortManagementEntity>().Where(x => x.Id == id)
                .FirstAsync();
            WebApplications.Add(entity.Id, await RunWebApi(entity));
        }
    }
}

public static class PortManagementExtension
{
    public static void MapPortManagement(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gateway/port-management/list",
                async (int page, int pageSize) =>
                    await PortManagementService.GetListAsync(page, pageSize))
            .RequireAuthorization();

        app.MapPost("/api/gateway/port-management",
            async ([FromServices]PortManagementService portManagementService, PortManagementEntity entity) =>
                await portManagementService.AddAsync(entity));

        app.MapPut("/api/gateway/port-management/enable/{id}",
            async ([FromServices]PortManagementService portManagementService, string id) =>
                await portManagementService.EnableAsync(id));
    }
}