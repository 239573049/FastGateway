using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;
using Directory = System.IO.Directory;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions()
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

builder.Services.AddResponseCompression(options => { options.Providers.Add<BrotliCompressionProvider>(); });

builder.Services.AddDbContext<MasterDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("Default");
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = "Data Source=/data/FastGateway.db";
    }

    if (!Directory.Exists("/data/"))
    {
        Directory.CreateDirectory("/data/");
    }

    options.UseSqlite(connectionString);
});

builder.Services
    .AddAuthorization()
    .AddJwtBearerAuthentication();

var app = builder.Build();

#region 数据库迁移

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
Console.WriteLine("数据库迁移中...");
await db.Database.MigrateAsync();
Console.WriteLine("数据库迁移完成");

await ProtectionService.LoadBlacklistAndWhitelistAsync(db);

await CertService.LoadCerts(db);

await ApiServiceService.LoadServices(db);

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
    AuthorizeService.CreateAsync);

#endregion

#region ApiService

var apiService = app.MapGroup("/api/v1/ApiService")
    .RequireAuthorization();

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

apiService.MapPost("/ServiceStats",
    ApiServiceService.ServiceStats);

apiService.MapPost("/CheckDirectoryExistence",
    ApiServiceService.CheckDirectoryExistenceAsync);

#endregion

#region Cert

var certService = app.MapGroup("/api/v1/Cert")
    .RequireAuthorization();

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
    .RequireAuthorization();

apiQpsService.MapGet(string.Empty,
    ApiQpsService.GetAsync);

#endregion

#region Statistic

var statisticService = app.MapGroup("/api/v1/Statistic")
    .RequireAuthorization();

statisticService.MapGet("/DayRequestCount",
    StatisticRequestService.GetDayStatisticAsync);

statisticService.MapGet("/TotalRequestCount",
    StatisticRequestService.GetTotalStatisticAsync);

statisticService.MapGet("/Location",
    StatisticRequestService.GetStatisticLocationAsync);

#endregion

#region Protection

var protectionService = app.MapGroup("/api/v1/Protection")
    .RequireAuthorization();

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

FastContext.SetQpsService(app.Services.GetRequiredService<IQpsService>(),
    app.Services.GetRequiredService<IMemoryCache>());

app.UseStaticFiles();

await app.RunAsync();