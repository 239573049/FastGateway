#region FreeSql类型转换

using Gateway.Middlewares.FlowAnalytics;

Utils.TypeHandlers.TryAdd(typeof(Dictionary<string, string>), new StringJsonHandler<Dictionary<string, string>>());
Utils.TypeHandlers.TryAdd(typeof(List<DestinationsEntity>), new StringJsonHandler<List<DestinationsEntity>>());
Utils.TypeHandlers.TryAdd(typeof(string[]), new StringJsonHandler<string[]>());

#endregion

var builder = WebApplication.CreateSlimBuilder(args);

var directory = new DirectoryInfo("/data");
if (!directory.Exists)
{
    directory.Create();
}

var freeSql = new FreeSqlBuilder()
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

builder.Services.AddSingleton<IFlowAnalyzer, FlowAnalyzer>();

builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Limits.MaxRequestBodySize = null;

    kestrel.ListenAnyIP(8081, portOptions =>
    {
        portOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
        portOptions.UseHttps();
        portOptions.Use<FlowAnalyzeMiddleware>();
    });

    kestrel.ListenAnyIP(8080, portOptions =>
    {
        portOptions.Protocols = HttpProtocols.Http1AndHttp2;
        portOptions.Use<FlowAnalyzeMiddleware>();
    });
});

#region Jwt

var policies = freeSql.Select<RouteEntity>().Where(a => a.AuthorizationPolicy != null).Distinct().ToList();

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

builder.Services.Configure<KestrelServerOptions>(options => { options.Limits.MaxRequestBodySize = int.MaxValue; });

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

builder.Services.AddHostedService<GatewayBackgroundService>();

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

builder.Services.AddSingleton<GatewayService>();
builder.Services.AddSingleton<CertificateService>();
builder.Services.AddSingleton<StaticFileProxyService>();
builder.Services.AddSingleton<TestService>();
builder.Services.AddSingleton<SystemService>();

builder.Services.AddSingleton<IContentTypeProvider, FileExtensionContentTypeProvider>();

builder.Services.AddSingleton<IFreeSql>(freeSql);

// 使用内存加载配置 
builder.Services.AddReverseProxy()
    .LoadFromMemory(GatewayService.Routes, GatewayService.Clusters)
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddTunnelServices();

var app = builder.Build();

app.UseCors("AllowAll");

app.UseMiddleware<GatewayMiddleware>();
app.UseMiddleware<StaticFileProxyMiddleware>();

// 配置MiniApis服务
app.MapStaticFileProxy();
app.MapFileStorage();
app.MapGateway();
app.MapAuthority();
app.MapCertificate();
app.MapSystem();
// 添加自定义授权
app.UseCustomAuthentication();

app.MapWebSocketTunnel("/api/gateway/connect-ws");

// Auth可以添加到这个端点，我们可以将它限制在某些点上
// 避免外部流量撞击它
app.MapHttp2Tunnel("/api/gateway/connect-h2");

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

await app.RunAsync();