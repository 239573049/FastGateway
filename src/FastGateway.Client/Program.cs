using FastGateway.Client.Options;
using FastGateway.Client.Transport;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Configuration.GetSection(TunnelClientOptions.Name)
    .Get<TunnelClientOptions>();

builder.WebHost.UseTunnelTransport();

var app = builder.Build();

app.MapReverseProxy();

app.Run();
