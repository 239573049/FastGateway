#region FreeSql类型转换

Utils.TypeHandlers.TryAdd(typeof(Dictionary<string, string>), new StringJsonHandler<Dictionary<string, string>>());
Utils.TypeHandlers.TryAdd(typeof(RouteMatchEntity), new StringJsonHandler<RouteMatchEntity>());
Utils.TypeHandlers.TryAdd(typeof(List<DestinationsEntity>), new StringJsonHandler<List<DestinationsEntity>>());

#endregion

var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddSingleton<IFreeSql>(_ =>
{
    var freeSql = new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseMonitorCommand(cmd => Console.WriteLine($"Sql：{cmd.CommandText}")) //监听SQL语句
        .UseAutoSyncStructure(true) //自动同步实体结构到数据库，FreeSql不会扫描程序集，只有CRUD时才会生成表。
        .Build();
    return freeSql;
});


builder.Services.AddReverseProxy()
    .LoadFromMemory(GatewayService.Routes, GatewayService.Clusters);

var app = builder.Build();

app.UseCors("AllowAll");

app.UseMiddleware<RequestLogMiddleware>();

app.MapGet("/api/gateway/panel", async (RequestLogService requestLogService, int hours) =>
    await requestLogService.PanelAsync(hours));

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

app.MapReverseProxy();

await app.RunAsync();