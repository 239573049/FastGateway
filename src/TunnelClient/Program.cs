using System.Runtime.InteropServices;
using TunnelClient;
using TunnelClient.Model;
using AppContext = System.AppContext;

var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions()
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

Directory.SetCurrentDirectory(AppContext.BaseDirectory);

// 读取args -c 配置文件路径
if (args.Length > 0 && args[0].StartsWith("-c", StringComparison.OrdinalIgnoreCase))
{
    var configFilePath = args[1].Trim();
    if (!string.IsNullOrEmpty(configFilePath) && File.Exists(configFilePath))
    {
        builder.Configuration.AddJsonFile(configFilePath, optional: false, reloadOnChange: true);
    }
    else
    {
        Console.WriteLine("配置文件不存在或路径错误: " + configFilePath);
        return;
    }
}
else
{
    throw new ArgumentException("请使用 -c 参数指定配置文件路径，参考：./FastGateway.TunnelClient -c ./tunnel.json");
}

if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    // Windows平台使用Windows服务
    builder.Services.AddWindowsService();
}

var (routeConfigs, clusterConfigs) = Tunnel.ToYarpOption();

builder.Services.AddReverseProxy()
    .LoadFromMemory(routeConfigs, clusterConfigs);

builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapReverseProxy();

await app.RunAsync("http://localhost:" + Tunnel.GetTunnel().Port);