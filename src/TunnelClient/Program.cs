using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using TunnelClient;
using TunnelClient.Model;
using AppContext = System.AppContext;

var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

Tunnel tunnel;
try
{
    tunnel = Tunnel.Load(args);
    tunnel.Validate();
}
catch (ArgumentException e)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(e.Message);
    Console.ResetColor();
    return;
}

var localPort = tunnel.ResolveLocalPort();

// 网关到客户端的请求链路固定使用 h2c。明文端点无法用 ALPN 协商，
// 必须显式只监听 HTTP/2，否则 Kestrel 会退回 HTTP/1.1 并导致 502。
builder.WebHost.ConfigureKestrel(options =>
    options.ListenLocalhost(localPort, listenOptions =>
        listenOptions.Protocols = HttpProtocols.Http2));

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    // Windows平台使用Windows服务
    builder.Services.AddWindowsService();
}

var (routeConfigs, clusterConfigs) = Tunnel.ToYarpOption(tunnel);

builder.Services.AddSingleton(tunnel);
builder.Services.AddReverseProxy()
    .LoadFromMemory(routeConfigs, clusterConfigs);

builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapReverseProxy();

var enabledProxyCount = tunnel.Proxy.Count(p => p.Enabled);
Console.WriteLine("========== FastGateway 隧道客户端 ==========");
Console.WriteLine($"  节点名称：{tunnel.Name}");
Console.WriteLine($"  服务器　：{tunnel.ServerUrl}");
Console.WriteLine($"  传输协议：{(tunnel.IsHttp2 ? "HTTP/2" : "WebSocket")}");
Console.WriteLine($"  代理规则：{enabledProxyCount} 条启用 / 共 {tunnel.Proxy.Length} 条");
Console.WriteLine($"  本地端口：{localPort}");
Console.WriteLine($"  心跳间隔：{tunnel.HeartbeatInterval}s，重连间隔：{tunnel.ReconnectInterval}s");
if (tunnel.InsecureSkipVerify)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine("  警告　　：已跳过 TLS 证书校验（InsecureSkipVerify），仅建议自签名证书环境使用");
    Console.ResetColor();
}

if (enabledProxyCount == 0)
    Console.WriteLine("  提示　　：当前没有启用的代理规则，节点上线后可在面板查看状态，稍后可补充规则");
Console.WriteLine("===========================================");

await app.RunAsync();
