using FastGateway.TypeHelper;
using FreeSql;
using FreeSql.Internal;
using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
using Directory = System.IO.Directory;

var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions()
{
    Args = args,
});

builder.Services.AddEnvironmentVariable();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder
            .SetIsOriginAllowed(_ => true)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonDateTimeConverter());
});

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISearcher>(new Searcher(CachePolicy.File, "ip2region.xdb"));
builder.Services.AddSingleton<IQpsService, QpsService>();
builder.Services.AddHostedService<RenewSslBackgroundService>();
builder.Services.AddHostedService<StatisticsBackgroundService>();

builder.Services.AddSingleton<IFreeSql>((_) =>
{
    if (!Directory.Exists("/data"))
    {
        Directory.CreateDirectory("/data");
    }

    var @default = builder.Configuration.GetConnectionString("Default");

    if (string.IsNullOrEmpty(@default))
    {
        @default = "Data Source=fast-gateway.db";
    }

    var freeSql = new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, @default)
#if DEBUG
        .UseMonitorCommand(cmd => Console.WriteLine($"Sql：{cmd.CommandText}"))
#endif
        .UseAutoSyncStructure(true)
        .Build();

    return freeSql;
});

builder.Services.AddResponseCompression();


builder.Services
    .AddAuthorization()
    .AddJwtBearerAuthentication();

var app = builder.Build();

#region 数据库迁移

using var scope = app.Services.CreateScope();
var freeSql = scope.ServiceProvider.GetRequiredService<IFreeSql>();

#region FreeSql类型转换

Utils.TypeHandlers.TryAdd(typeof(Dictionary<string, string>),
    new StringJsonHandler<Dictionary<string, string>>());

Utils.TypeHandlers.TryAdd(typeof(string[]),
    new StringHandler());

Utils.TypeHandlers.TryAdd(typeof(List<UpStream>),
    new UpStreamHandler());

Utils.TypeHandlers.TryAdd(typeof(List<CertData>),
    new CertDataHandler());

Utils.TypeHandlers.TryAdd(typeof(List<string>),
    new StringJsonHandler<List<string>>());

Utils.TypeHandlers.TryAdd(typeof(List<LocationService>),
    new LocationServiceHandler());

Utils.TypeHandlers.TryAdd(typeof(List<GeneralRules>)
    , new StringJsonHandler<List<GeneralRules>>());

#endregion

freeSql.CodeFirst.SyncStructure<Service>();
freeSql.CodeFirst.SyncStructure<BlacklistAndWhitelist>();
freeSql.CodeFirst.SyncStructure<Cert>();
freeSql.CodeFirst.SyncStructure<StatisticRequestCount>();
freeSql.CodeFirst.SyncStructure<StatisticIp>();
freeSql.CodeFirst.SyncStructure<Location>();
freeSql.CodeFirst.SyncStructure<RateLimit>();

await ProtectionService.LoadBlacklistAndWhitelistAsync(freeSql);

await CertService.LoadCerts(freeSql);

await ApiServiceService.LoadServices(freeSql);

#endregion

app.UseResponseCompression();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    // 如果是/则重定向到index.html
    if (context.Request.Path == "/")
    {
        context.Request.Path = "/index.html";
    }

    await next(context);

    if (context.Response.StatusCode == 404)
    {
        context.Request.Path = "/index.html";
        await next(context);
    }
});

#region Authorize

var authorizeService = app.MapGroup("/api/v1/Authorize");

authorizeService.MapPost(string.Empty,
        AuthorizeService.CreateAsync)
    .AddEndpointFilter<ExceptionFilter>();

#endregion

#region ApiService

var apiService = app.MapGroup("/api/v1/ApiService")
    .RequireAuthorization()
    .AddEndpointFilter<ExceptionFilter>();

apiService.MapGet("/List",
    ApiServiceService.GetListAsync);

apiService.MapGet(string.Empty,
    ApiServiceService.GetAsync);

apiService.MapPost(string.Empty,
    ApiServiceService.CreateAsync);

apiService.MapPut(string.Empty,
    ApiServiceService.UpdateAsync);

apiService.MapDelete(string.Empty,
    ApiServiceService.DeleteAsync);

apiService.MapPost("/StartService",
    ApiServiceService.StartServiceAsync);

apiService.MapPost("/StopService",
    ApiServiceService.StopServiceAsync);

apiService.MapPost("/RestartService",
    ApiServiceService.RestartServiceAsync);

apiService.MapPost("/RestartConfig/{id}",
    ApiServiceService.RestartConfigAsync);

apiService.MapGet("/ClientConnect",
    ApiServiceService.ClientConnect);

apiService.MapPost("/ServiceStats",
    ApiServiceService.ServiceStats);

apiService.MapPost("/CheckDirectoryExistence",
    ApiServiceService.CheckDirectoryExistenceAsync);

#endregion

#region Cert

var certService = app.MapGroup("/api/v1/Cert")
    .RequireAuthorization()
    .AddEndpointFilter<ExceptionFilter>();

certService.MapGet("/List",
    CertService.GetListAsync);

certService.MapGet(string.Empty,
    CertService.GetAsync);

certService.MapPost(string.Empty,
    CertService.CreateAsync);

certService.MapPut(string.Empty,
    CertService.UpdateAsync);

certService.MapDelete(string.Empty,
    CertService.DeleteAsync);

certService.MapPost("/Apply",
    CertService.ApplyAsync);

#endregion

#region Qps

var apiQpsService = app.MapGroup("/api/v1/Qps")
    .RequireAuthorization()
    .AddEndpointFilter<ExceptionFilter>();

apiQpsService.MapGet(string.Empty,
    ApiQpsService.GetAsync);

#endregion

#region Statistic

var statisticService = app.MapGroup("/api/v1/Statistic")
    .RequireAuthorization()
    .AddEndpointFilter<ExceptionFilter>();

statisticService.MapGet("/DayRequestCount",
    StatisticRequestService.GetDayStatisticAsync);

statisticService.MapGet("/TotalRequestCount",
    StatisticRequestService.GetTotalStatisticAsync);

statisticService.MapGet("/Location",
    StatisticRequestService.GetStatisticLocationAsync);

statisticService.MapGet("/DayStatisticLocationCount",
    StatisticRequestService.GetDayStatisticLocationCountAsync);

#endregion

#region Protection

var protectionService = app.MapGroup("/api/v1/Protection")
    .RequireAuthorization()
    .AddEndpointFilter<ExceptionFilter>();

protectionService.MapPost("/BlacklistAndWhitelist",
    ProtectionService.CreateBlacklistAndWhitelistAsync);

protectionService.MapGet("/BlacklistAndWhitelist/List",
    ProtectionService.GetBlacklistListAsync);

protectionService.MapDelete("/BlacklistAndWhitelist/{id}",
    ProtectionService.DeleteBlacklistAsync);

protectionService.MapPut("/BlacklistAndWhitelist",
    ProtectionService.UpdateBlacklistAsync);

protectionService.MapPost("/BlacklistAndWhitelist/Enable/{id}",
    ProtectionService.EnableBlacklistAndWhitelistAsync);

#endregion


#region RateLimit

var rateLimitService = app.MapGroup("/api/v1/RateLimit")
    .RequireAuthorization()
    .AddEndpointFilter<ExceptionFilter>();

rateLimitService.MapPost(string.Empty,
    RateLimitService.CreateAsync);

rateLimitService.MapPut("{name}",
    RateLimitService.UpdateAsync);

rateLimitService.MapDelete("{name}",
    RateLimitService.DeleteAsync);

rateLimitService.MapGet("/List",
    RateLimitService.GetListAsync);

rateLimitService.MapGet("/Names",
    RateLimitService.GetNamesAsync);

#endregion

FastContext.SetQpsService(app.Services.GetRequiredService<IQpsService>(),
    app.Services.GetRequiredService<IMemoryCache>());

app.UseStaticFiles();

await app.RunAsync();