using System.Diagnostics;
using FreeSql;
using FreeSql.Internal;
using Gateway.Entities;
using Gateway.Options;
using Gateway.Services;
using Gateway.TypeHandlers;

Utils.TypeHandlers.TryAdd(typeof(Dictionary<string, string>), new StringJsonHandler());

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.GetSection(nameof(RequestOptions)).Get<RequestOptions>();

builder.Services.AddSingleton<RequestLogService>();
builder.Services.AddSingleton<IFreeSql>((_ =>
{
    var freeSql = new FreeSqlBuilder()
        .UseConnectionString(DataType.Sqlite, builder.Configuration.GetConnectionString("DefaultConnection"))
        .UseMonitorCommand(cmd => Console.WriteLine($"Sql：{cmd.CommandText}")) //监听SQL语句
        .UseAutoSyncStructure(true) //自动同步实体结构到数据库，FreeSql不会扫描程序集，只有CRUD时才会生成表。
        .Build();
    return freeSql;
}));

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.Use((async (context, next) =>
{
    if (RequestOptions.FilterSuffixes.Any(x => context.Request.Path.Value?.EndsWith(x) == true))
        return;

    var requestLogService = context.RequestServices.GetRequiredService<RequestLogService>();
    var ip = context.Connection.RemoteIpAddress?.ToString();

    var requestLog = new RequestLog
    {
        Path = context.Request.Path,
        Method = context.Request.Method,
        QueryString = context.Request.QueryString.ToString(),
        CreatedTime = DateTime.Now,
        Ip = ip,
        BrowserInfo = context.Request.Headers.UserAgent.ToString(),
    };
    context.Items["RequestLog"] = requestLog;
    var stopwatch = Stopwatch.StartNew();
    await next();
    stopwatch.Stop();

    // 过滤Content-Type
    if (RequestOptions.FilterContentTypes.Any(x => context.Response.ContentType?.Contains(x) == true))
        return;

    requestLog.ExecutionDuration = stopwatch.Elapsed.TotalMilliseconds;
    requestLog.StatusCode = context.Response.StatusCode;
    await requestLogService.LogAsync(requestLog);
}));

app.MapGet("/api/panel", async (RequestLogService requestLogService, int hours) =>
    await requestLogService.PanelAsync(hours));

app.MapReverseProxy();

await app.RunAsync();