using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FastGateway.Entities;
using FastGateway.TunnelClient.Monitor;

namespace FastGateway.TunnelClient
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly IServiceProvider _services;

        public Worker(ILogger<Worker> logger, IServiceProvider services)
        {
            _logger = logger;
            _services = services;
        }

        [UnconditionalSuppressMessage("Trimming",
            "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
            Justification = "<Pending>")]
        [UnconditionalSuppressMessage("AOT",
            "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
            Justification = "<Pending>")]
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 读取配置文件
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tunnel.json");

            var json = await File.ReadAllTextAsync(path, stoppingToken);

            var tunnel = JsonSerializer.Deserialize<Tunnel>(json, AppContext.JsonOptions);

            if (tunnel == null)
            {
                _logger.LogError("Tunnel configuration is null. Please check the tunnel.json file.");
                throw new InvalidOperationException("Tunnel configuration is null. Please check the tunnel.json file.");
            }

            var monitorServer = new MonitorServer();

            var serverClient = new ServerClient(monitorServer, tunnel,
                _services.GetRequiredService<ILogger<ServerClient>>());

            await MonitorServer.RegisterNodeAsync(tunnel, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                await MonitorServerAsync(serverClient, tunnel, stoppingToken);
                _logger.LogInformation("尝试重新连接到服务器...");
                await Task.Delay(tunnel.ReconnectInterval, stoppingToken);
                _logger.LogInformation("重新连接到服务器中...");
            }
        }

        private async Task MonitorServerAsync(ServerClient serverClient,
            Tunnel tunnel, CancellationToken stoppingToken)
        {
            try
            {
                await serverClient.TransportCoreAsync(tunnel, stoppingToken);
            }
            catch (UnauthorizedAccessException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e);
                Console.ResetColor();
                _logger.LogError(e, "连接被拒绝，请检查Token是否正确或是否过期！");
                await Task.Delay(1000, stoppingToken);
                Environment.Exit(0);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "连接错误！");
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}