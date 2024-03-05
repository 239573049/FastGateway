using FastGateway.Client.Options;
using FastGateway.Client.Transport;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Configuration.GetSection(TunnelClientOptions.Name)
    .Get<TunnelClientOptions>();

var TUNNEL_URL = Environment.GetEnvironmentVariable("TUNNEL_URL");
if (!string.IsNullOrEmpty(TUNNEL_URL))
{
    TunnelClientOptions.Url = TUNNEL_URL;
}

builder.WebHost.UseTunnelTransport();

var app = builder.Build();

app.MapReverseProxy();

app.Run();
