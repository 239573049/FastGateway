using FastGateway.TunnelClient;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<Worker>();

var host = builder.Build();

await host.RunAsync();
