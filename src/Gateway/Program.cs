using System.Reflection;
using Gateway.Middlewares.FlowAnalytics;

namespace Gateway;

internal static class Program
{
    private static IFreeSql _freeSql;

    public static async Task Main(string[] args)
    {
        // 获取当前程序集版本
        var version = Assembly.GetExecutingAssembly().GetName().Version;

        var title = @"
           ______________
       ,===:'.,            `-._
            `:.`---.__         `-._
              `:.     `--.         `.
                \.        `.         `.
        (,,(,    \.         `.   ____,-`.,
     (,'     `/   \.   ,--.___`.'
 ,  ,'  ,--.  `,   \.;'         `
  `{D, {    \  :    \;
    V,,'    /  /    //
    j;;    /  ,' ,-//.    ,---.      ,
    \;'   /  ,' /  _  \  /  _  \   ,'/
          \   `'  / \  `'  / \  `.' /
           `.___,'   `.__,'   `.__,'  
神龙保佑,代码无BUG!
[欢迎使用Token Gateway]
[版本：v{version}]
[作者：token]
";
        title = title.Replace("{version}", version?.ToString() ?? "1.0.0");

        // 给控制台输出一个彩色的标题
        Console.ForegroundColor = ConsoleColor.Green;

        Console.WriteLine(title);

        Console.ResetColor();

        #region FreeSql类型转换

        Utils.TypeHandlers.TryAdd(typeof(Dictionary<string, string>),
            new StringJsonHandler<Dictionary<string, string>>());
        Utils.TypeHandlers.TryAdd(typeof(List<DestinationsEntity>), new StringJsonHandler<List<DestinationsEntity>>());
        Utils.TypeHandlers.TryAdd(typeof(string[]), new StringJsonHandler<string[]>());

        #endregion

        var builder = WebApplication.CreateBuilder(args);

        var directory = new DirectoryInfo("/data");
        if (!directory.Exists)
        {
            directory.Create();
        }

        _freeSql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, builder.Configuration.GetConnectionString("DefaultConnection"))
            .UseMonitorCommand(cmd => Console.WriteLine($"Sql：{cmd.CommandText}")) //监听SQL语句
            .UseAutoSyncStructure(true) //自动同步实体结构到数据库，FreeSql不会扫描程序集，只有CRUD时才会生成表。
            .Build();


        builder.Configuration.GetSection(nameof(JwtOptions))
            .Get<JwtOptions>();

        builder.Configuration.GetSection(GatewayOptions.Name)
            .Get<GatewayOptions>();

        // 获取环境变量
        var https_password = Environment.GetEnvironmentVariable("HTTPS_PASSWORD") ?? "dd666666";
        var https_file = Environment.GetEnvironmentVariable("HTTPS_FILE") ?? "gateway.pfx";
        var enable_flow_monitoring = Environment.GetEnvironmentVariable("ENABLE_FLOW_MONITORING") ?? "true";


        builder.WebHost.UseKestrel(options =>
        {
            // 配置多个域名证书
            options.ConfigureHttpsDefaults(adapterOptions =>
            {
                adapterOptions.ServerCertificateSelector = (_, name) =>
                {
                    // 从Certificate服务中获取
                    if (string.IsNullOrEmpty(name) ||
                        !CertificateService.CertificateEntityDict.TryGetValue(name, out var certificate))
                    {
                        // 创建一个默认的证书
                        return new X509Certificate2(Path.Combine(AppContext.BaseDirectory, "certificates", https_file),
                            https_password);
                    }

                    var path = Path.Combine("/data/", certificate.Path);

                    if (File.Exists(path)) return new X509Certificate2(path, certificate.Password);

                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"证书文件不存在：{path}");
                    Console.ResetColor();

                    return new X509Certificate2(Path.Combine(AppContext.BaseDirectory, "certificates", https_file),
                        https_password);
                };
            });
        });

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Limits.MaxRequestBodySize = null;

            kestrel.ListenAnyIP(8081, portOptions =>
            {
                portOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                portOptions.UseHttps();

                if (enable_flow_monitoring == "true")
                {
                    portOptions.Use<FlowAnalyzeMiddleware>();
                }
            });

            kestrel.ListenAnyIP(8080, portOptions =>
            {
                portOptions.Protocols = HttpProtocols.Http1AndHttp2;

                if (enable_flow_monitoring == "true")
                {
                    portOptions.Use<FlowAnalyzeMiddleware>();
                }
            });
        });

        #region Jwt

        var policies = _freeSql.Select<RouteEntity>().Where(a => a.AuthorizationPolicy != null).Distinct().ToList();

        builder.Services
            .AddAuthorization(options =>
            {
                // 添加授权策略
                foreach (var policy in policies)
                {
                    options.AddPolicy(policy.AuthorizationPolicy!, policy => policy.RequireAuthenticatedUser());
                }
            })
            .AddJwtBearerAuthentication(policies);

        #endregion

        builder.Services.Configure<KestrelServerOptions>(options =>
        {
            options.Limits.MaxRequestBodySize = int.MaxValue;
        });

        builder.Services.Configure<FormOptions>(x =>
        {
            x.ValueLengthLimit = int.MaxValue;
            x.MultipartBodyLengthLimit = int.MaxValue; // if don't set default value is: 128 MB
            x.MultipartHeadersLengthLimit = int.MaxValue;
        });

        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonDateTimeConverter());
        });

        builder.Services.AddHostedService<FlowBackgroundService>();

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

        builder.Services.AddSingleton<TestService>();

        builder.Services.AddSingleton<IContentTypeProvider, FileExtensionContentTypeProvider>();

        builder.Services.AddSingleton<IFreeSql>(_freeSql);

        // 使用内存加载配置 
        builder.Services.AddReverseProxy()
            .LoadFromMemory(GatewayService.Routes, GatewayService.Clusters)
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

        builder.Services.AddTunnelServices();

        builder.Services.AddSingleton<IFlowAnalyzer, FlowAnalyzer>();

        var app = builder.Build();

        app.UseCors("AllowAll");

        app.UseMiddleware<GatewayMiddleware>();
        app.UseMiddleware<StaticFileProxyMiddleware>();

        // 添加自定义授权
        app.UseCustomAuthentication();

        app.MapWebSocketTunnel("/api/gateway/connect-ws");

        // Auth可以添加到这个端点，我们可以将它限制在某些点上
        // 避免外部流量撞击它
        app.MapHttp2Tunnel("/api/gateway/connect-h2");


        app.MapReverseProxy();

        _ = Task.Run(() => GatewayAdmin(args, app.Services));

        await app.RunAsync();
    }

    private static async Task GatewayAdmin(string[] args, IServiceProvider serviceProvider)
    {
        var flowAnalyzer = serviceProvider.GetRequiredService<IFlowAnalyzer>();
        var inMemoryConfigProvider = serviceProvider.GetRequiredService<InMemoryConfigProvider>();

        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAll",
                policyBuilder => policyBuilder
                    .SetIsOriginAllowed(_ => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
        });

        builder.Services.AddSingleton<GatewayService>();
        builder.Services.AddSingleton<CertificateService>();
        builder.Services.AddSingleton<StaticFileProxyService>();
        builder.Services.AddSingleton<IFlowAnalyzer>(flowAnalyzer);
        builder.Services.AddSingleton<InMemoryConfigProvider>(inMemoryConfigProvider);

        builder.Services.AddHostedService<GatewayBackgroundService>();

        builder.Services.AddSingleton<IFreeSql>(_freeSql);

        builder.Services.AddResponseCompression();

        builder.Services
            .AddAuthorization()
            .AddJwtBearerAuthentication();

        var app = builder.Build();

        app.UseCors("AllowAll");

        app.Use((async (context, next) =>
        {
            if (context.Request.Path == "/")
            {
                context.Request.Path = "/index.html";
            }

            await next(context);
        }));

        // 配置MiniApis服务
        app.MapStaticFileProxy();
        app.MapFileStorage();
        app.MapGateway();
        app.MapAuthority();
        app.MapCertificate();
        app.MapSystem();

        app.UseResponseCompression();

        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        await app.RunAsync();
    }
}