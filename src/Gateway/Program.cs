#region FreeSql类型转换

Utils.TypeHandlers.TryAdd(typeof(Dictionary<string, string>), new StringJsonHandler<Dictionary<string, string>>());
Utils.TypeHandlers.TryAdd(typeof(RouteMatchEntity), new StringJsonHandler<RouteMatchEntity>());
Utils.TypeHandlers.TryAdd(typeof(List<DestinationsEntity>), new StringJsonHandler<List<DestinationsEntity>>());

#endregion

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.GetSection(nameof(JwtOptions))
    .Get<JwtOptions>();

builder.WebHost.UseKestrel(options =>
{
    // 配置多个域名证书
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.ServerCertificateSelector = (_, name) =>
        {
            // 从Certificate服务中获取
            if (CertificateService.CertificateEntityDict.TryGetValue(name, out var certificate))
            {
                var path = Path.Combine(Directory.GetCurrentDirectory(), certificate.Path);

                if (File.Exists(path) == false)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"证书文件不存在：{path}");
                    Console.ResetColor();
                    throw new Exception($"证书文件不存在：{path}");
                }

                return new X509Certificate2(path, certificate.Password);
            }

            return null;
        };
    });
});

#region Jwt

builder.Services
    .AddAuthorization()
    .AddJwtBearerAuthentication();

#endregion

builder.Services.AddHostedService<GatewayBackgroundService>();

builder.Configuration.GetSection(nameof(RequestOptions)).Get<RequestOptions>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<RequestLogMiddleware>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

builder.Services.AddSingleton<RequestLogService>();
builder.Services.AddSingleton<GatewayService>();
builder.Services.AddSingleton<CertificateService>();
builder.Services.AddSingleton<FileStorageService>();
builder.Services.AddSingleton<TestService>();
builder.Services.AddSingleton<AuthorityService>();

builder.Services.AddSingleton<IFreeSql>(_ =>
{
    var freeSql = new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseMonitorCommand(cmd => Console.WriteLine($"Sql：{cmd.CommandText}")) //监听SQL语句
        .UseAutoSyncStructure(true) //自动同步实体结构到数据库，FreeSql不会扫描程序集，只有CRUD时才会生成表。
        .Build();
    return freeSql;
});

// 使用内存加载配置 
builder.Services.AddReverseProxy()
    .LoadFromMemory(GatewayService.Routes, GatewayService.Clusters);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseMiddleware<RequestLogMiddleware>();

app.MapGet("/api/gateway/panel", async (RequestLogService requestLogService, int hours) =>
    await requestLogService.PanelAsync(hours));

app.MapGet("/api/gateway/request-log", async (RequestLogService requestLogService, string keyword, int page, int pageSize,
        DateTime? startTime, DateTime? endTime) =>
    await requestLogService.GetListAsync(keyword, page, pageSize, startTime, endTime));

app.MapPut("/api/gateway/refresh-config", async (GatewayService gatewayService) =>
    await gatewayService.RefreshConfig());

#region Router

app.MapGet("/api/gateway/routes", async (GatewayService gatewayService) =>
    await gatewayService.GetRouteAsync());

app.MapPost("/api/gateway/routes", async (GatewayService gatewayService, RouteEntity routeEntity) =>
    await gatewayService.CreateRouteAsync(routeEntity));

app.MapPut("/api/gateway/routes", async (GatewayService gatewayService, RouteEntity routeEntity) =>
    await gatewayService.UpdateRouteAsync(routeEntity));

app.MapDelete("/api/gateway/routes/{routeId}", async (GatewayService gatewayService, string routeId) =>
    await gatewayService.DeleteRouteAsync(routeId));

#endregion

#region Clusters

app.MapGet("/api/gateway/clusters", async (GatewayService gatewayService) =>
    await gatewayService.GetClusterAsync());

app.MapPost("/api/gateway/clusters", async (GatewayService gatewayService, ClusterEntity clusterEntity) =>
    await gatewayService.CreateClusterAsync(clusterEntity));

app.MapPut("/api/gateway/clusters", async (GatewayService gatewayService, ClusterEntity clusterEntity) =>
    await gatewayService.UpdateClusterAsync(clusterEntity));

app.MapDelete("/api/gateway/clusters/{clusterId}", async (GatewayService gatewayService, string clusterId) =>
    await gatewayService.DeleteClusterAsync(clusterId));

#endregion

#region FileStorage

app.MapPost("/api/gateway/file-storage", async (FileStorageService fileStorageService, HttpContext context) =>
    await fileStorageService.UploadAsync(context));

#endregion

#region Certificate

app.MapGet("/api/gateway/certificates", async (CertificateService certificateService) =>
    await certificateService.GetAsync());

app.MapPost("/api/gateway/certificates",
    async (CertificateService certificateService, CertificateEntity certificateEntity) =>
        await certificateService.CreateAsync(certificateEntity));

app.MapPut("/api/gateway/certificates",
    async (CertificateService certificateService, CertificateEntity certificateEntity) =>
        await certificateService.UpdateAsync(certificateEntity));

app.MapDelete("/api/gateway/certificates/{id}", async (CertificateService certificateService, string id) =>
    await certificateService.DeleteAsync(id));

app.MapPut("/api/gateway/certificates/{id}", async (string id, string path, CertificateService certificateService) =>
    await certificateService.UpdateCertificateAsync(id, path));

#endregion

#region AuthorityService

app.MapPost("/api/gateway/token", async (AuthorityService authorityService, string username, string password) =>
    await authorityService.GetTokenAsync(username, password));

#endregion

app.UseAuthentication();
app.UseAuthorization();

app.MapReverseProxy();

await app.RunAsync();