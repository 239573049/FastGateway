using FastGateway;
using FastGateway.BackgroundServices;
using FastGateway.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.GetSection("JwtOptions").Get<JwtOptions>();

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

builder.Services.AddSingleton<IQpsService, QpsService>();
builder.Services.AddHostedService<RenewSslBackgroundService>();
builder.Services.AddHostedService<StatisticsBackgroundService>();

builder.Services.AddMapster();

builder.Services.AddDbContext<MasterDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("Default"));
});

builder.Services
    .AddAuthorization()
    .AddJwtBearerAuthentication();

var app = builder.Build();


app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/.well-known/acme-challenge/{token}", AcmeChallenge.Challenge);
app.MapPost("/.well-known/acme-challenge/{token}", AcmeChallenge.Challenge);

#region Authorize

var authorizeService = app.MapGroup("/api/v1/Authorize")
    .RequireAuthorization();

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

apiService.MapGet("/SelectList",
    ApiServiceService.GetSelectListAsync);

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

#endregion

FastContext.SetQpsService(app.Services.GetRequiredService<IQpsService>());

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
    await db.Database.MigrateAsync();

    await ApiServiceService.LoadServices(db);
    
    await CertService.LoadCerts(db);
}

await app.RunAsync();