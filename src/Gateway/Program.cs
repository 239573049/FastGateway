using CoreFlex.Jwt;
using CoreFlex.Module.Extensions;
using Gateway;
using Gateway.Services;
using LiteDB;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.GetSection(nameof(JwtOptions)).Get<JwtOptions>();

await builder.AddCoreFlexAutoInjectAsync<GatewayModule>();

builder.Services.AddSingleton<ILiteDatabase>(_ =>
{
    var dbPath = Path.Combine(builder.Environment.ContentRootPath, "gateway.db");
    return new LiteDatabase(dbPath);
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapReverseProxy();

app.MapPost("/api/authority/token", async (AuthorityService service, string userName, string password) =>
    await service.GetTokenAsync(userName, password));

await app.Services.UseCoreFlexAsync();

await app.RunAsync();