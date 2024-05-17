using FastGateway.TunnelClient;

var builder = WebApplication.CreateBuilder();

var gateway= builder.Configuration["Gateway"];

if (string.IsNullOrEmpty(gateway))
{
    Console.WriteLine("Gateway is empty");
    return;
}

builder.WebHost.UseTunnelTransport(gateway);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.MapReverseProxy();

await app.RunAsync();