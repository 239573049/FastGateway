using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Core;
using Core.Entities;
using FastGateway.Services;

namespace FastGateway.Cluster;

/// <summary>
///     从节点内嵌的集群数据面隧道客户端（移植 TunnelClient 的连接核心，WebSocket 传输）。
///     角色为 Worker 时按「启用服务的监听端口」逐一向主网关注册 cluster-{nodeId}-{port}，
///     主网关中继过来的请求经隧道桥接回本机对应网关端口，重新走本节点的路由转发。
/// </summary>
public class ClusterRelayAgentService(ClusterStateService stateService, ConfigurationService configService)
    : BackgroundService
{
    private static readonly ReadOnlyMemory<byte> PingLine = "PING\r\n"u8.ToArray();
    private static readonly ReadOnlyMemory<byte> PongLine = "PONG\r\n"u8.ToArray();

    private readonly ConcurrentDictionary<int, CancellationTokenSource> _connections = new();
    private string? _identitySignature;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Reconcile(stoppingToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"集群隧道客户端调和失败：{ex.Message}");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    /// <summary>按当前角色与服务列表启停各端口的注册连接</summary>
    private void Reconcile(CancellationToken stoppingToken)
    {
        var state = stateService.Get();

        var active = state.Role == ClusterRole.Worker
                     && !string.IsNullOrEmpty(state.MasterEndpoint)
                     && !string.IsNullOrEmpty(state.NodeId)
                     && !string.IsNullOrEmpty(state.NodeToken);

        var signature = active ? $"{state.MasterEndpoint}|{state.NodeId}|{state.NodeToken}" : null;

        // 身份（主网关地址/节点凭证）变化：全部重连
        if (signature != _identitySignature)
        {
            _identitySignature = signature;
            StopAll();
        }

        if (!active) return;

        var desiredPorts = configService.GetServers()
            .Where(s => s.Enable)
            .Select(ClusterRelay.RelayPort)
            .ToHashSet();

        foreach (var (port, cts) in _connections)
            if (!desiredPorts.Contains(port))
            {
                if (_connections.TryRemove(port, out _)) cts.Cancel();
            }

        foreach (var port in desiredPorts)
        {
            if (_connections.ContainsKey(port)) continue;

            var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            if (!_connections.TryAdd(port, cts)) continue;

            var endpoint = state.MasterEndpoint!;
            var nodeName = ClusterRelay.TunnelNodeName(state.NodeId!, port);
            var token = state.NodeToken!;

            _ = Task.Run(() => MaintainConnectionAsync(endpoint, nodeName, token, port, cts.Token), cts.Token);
        }
    }

    private void StopAll()
    {
        foreach (var (port, cts) in _connections)
            if (_connections.TryRemove(port, out _))
                cts.Cancel();
    }

    /// <summary>单端口注册主连接：断线 5s 重连，直到被调和逻辑取消</summary>
    private static async Task MaintainConnectionAsync(string masterEndpoint, string nodeName, string token,
        int localPort, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ConnectAndServeAsync(masterEndpoint, nodeName, token, localPort, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"集群隧道 {nodeName} 连接中断：{ex.Message}");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static async Task ConnectAndServeAsync(string masterEndpoint, string nodeName, string token,
        int localPort, CancellationToken cancellationToken)
    {
        var mainUri = BuildTunnelUri(masterEndpoint, $"nodeName={Uri.EscapeDataString(nodeName)}&token={Uri.EscapeDataString(token)}");

        await using var stream = new SafeWriteStream(await ConnectWebSocketAsync(mainUri, cancellationToken));

        Console.WriteLine($"集群隧道已注册：{nodeName} → {masterEndpoint}");

        // 客户端侧心跳：探测半开连接，服务端收到 PING 会回 PONG
        using var pingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pingTask = Task.Run(async () =>
        {
            while (!pingCts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), pingCts.Token);
                await stream.WriteAsync(PingLine, pingCts.Token);
            }
        }, pingCts.Token);

        try
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            while (!cancellationToken.IsCancellationRequested)
            {
                // 服务端心跳周期 50s，读超时略放宽用于识别死连接
                var line = await reader.ReadLineAsync(cancellationToken).AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(90), cancellationToken);

                if (line == null) return;

                if (line == "PING")
                    await stream.WriteAsync(PongLine, cancellationToken);
                else if (line == "PONG")
                {
                    // 心跳回应，无需处理
                }
                else if (Guid.TryParse(line, out var tunnelId))
                    _ = BridgeTunnelAsync(masterEndpoint, tunnelId, localPort, cancellationToken);
            }
        }
        finally
        {
            pingCts.Cancel();
            try
            {
                await pingTask;
            }
            catch
            {
                // 心跳收尾异常属预期
            }
        }
    }

    /// <summary>
    ///     数据回连：向主网关开一条 tunnelId 流，并桥接到本机网关端口，双向拷贝直至任一方向结束。
    /// </summary>
    private static async Task BridgeTunnelAsync(string masterEndpoint, Guid tunnelId, int localPort,
        CancellationToken cancellationToken)
    {
        try
        {
            var tunnelUri = BuildTunnelUri(masterEndpoint, $"tunnelId={tunnelId}");

            await using var serverStream =
                new ForceFlushStream(await ConnectWebSocketAsync(tunnelUri, cancellationToken));

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new DnsEndPoint("127.0.0.1", localPort), cancellationToken);
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            await using var localStream = new NetworkStream(socket, true);

            using var copyCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var serverToLocal = serverStream.CopyToAsync(localStream, copyCts.Token);
            var localToServer = localStream.CopyToAsync(serverStream, copyCts.Token);

            await Task.WhenAny(serverToLocal, localToServer);

            // 一方结束后取消另一方向，并等待两个拷贝都退出再释放流，避免无人观察的异常
            copyCts.Cancel();
            try
            {
                await Task.WhenAll(serverToLocal, localToServer);
            }
            catch
            {
                // 取消收尾产生的异常属预期
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"集群隧道 {tunnelId} 桥接异常：{ex.Message}");
        }
    }

    private static Uri BuildTunnelUri(string masterEndpoint, string query)
    {
        var endpoint = masterEndpoint.TrimEnd('/');
        var wsScheme = endpoint.StartsWith("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";
        return new Uri(
            $"{wsScheme}{endpoint[endpoint.IndexOf("://", StringComparison.Ordinal)..]}{ClusterTunnelHub.EndpointPath}?{query}");
    }

    private static async Task<Stream> ConnectWebSocketAsync(Uri uri, CancellationToken cancellationToken)
    {
        var webSocket = new ClientWebSocket();
        webSocket.Options.AddSubProtocol(Constant.Protocol);
        webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(30);

        try
        {
            await webSocket.ConnectAsync(uri, cancellationToken);
            return new CustomWebSocketStream(webSocket);
        }
        catch
        {
            webSocket.Dispose();
            throw;
        }
    }
}
