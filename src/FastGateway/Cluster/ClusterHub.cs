using System.Collections.Concurrent;
using System.Net.WebSockets;
using FastGateway.Infrastructure;
using FastGateway.Services;

namespace FastGateway.Cluster;

/// <summary>
///     主网关侧集群中枢：维护从节点同步长连接，配置变更后向所有在线节点推送全量快照。
/// </summary>
public static class ClusterHub
{
    private static readonly ConcurrentDictionary<string, ClusterConnection> Connections = new();

    private static ConfigurationService? _configService;
    private static ClusterStateService? _stateService;
    private static CancellationTokenSource? _debounceCts;

    private sealed class ClusterConnection(string nodeId, WebSocket socket)
    {
        public string NodeId { get; } = nodeId;
        public WebSocket Socket { get; } = socket;
        public SemaphoreSlim SendLock { get; } = new(1, 1);
        public DateTime LastSeen { get; set; } = DateTime.Now;
        public long SyncedVersion { get; set; }
    }

    /// <summary>启动时注入依赖并订阅配置变更（进程内只调用一次）</summary>
    public static void Initialize(ConfigurationService configService, ClusterStateService stateService)
    {
        _configService = configService;
        _stateService = stateService;

        ConfigurationService.ConfigurationChanged += QueueBroadcast;
    }

    public static bool IsNodeOnline(string nodeId)
    {
        return Connections.ContainsKey(nodeId);
    }

    public static (DateTime? lastSeen, long syncedVersion) GetNodeStatus(string nodeId)
    {
        return Connections.TryGetValue(nodeId, out var conn)
            ? (conn.LastSeen, conn.SyncedVersion)
            : (null, 0);
    }

    /// <summary>移除节点时断开其同步通道（先发 removed 通知，从节点收到后自动退出集群）</summary>
    public static async Task DisconnectNodeAsync(string nodeId)
    {
        if (!Connections.TryRemove(nodeId, out var conn)) return;

        try
        {
            await SendAsync(conn, new ClusterMessage { Type = ClusterMessage.TypeRemoved });
            await conn.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "removed", CancellationToken.None);
        }
        catch
        {
            // 连接可能已断开
        }
    }

    /// <summary>
    ///     处理从节点的同步 WebSocket 连接（匿名端点，凭 nodeId + nodeToken 鉴权）
    /// </summary>
    public static async Task HandleNodeConnectionAsync(HttpContext context)
    {
        if (_configService == null || _stateService == null)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        var nodeId = context.Request.Query["nodeId"].ToString();
        var token = context.Request.Query["token"].ToString();

        var node = string.IsNullOrEmpty(nodeId) ? null : _stateService.FindNode(nodeId);
        if (node == null || string.IsNullOrEmpty(token) || !ClusterStateService.TokenEquals(node.Token, token))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var connection = new ClusterConnection(nodeId, socket);
        GatewayResourceMetrics.WebSocketOpened();

        // 同一节点重连时顶掉旧连接
        if (Connections.TryRemove(nodeId, out var stale))
        {
            try
            {
                stale.Socket.Abort();
            }
            catch
            {
                // ignore
            }
        }

        Connections[nodeId] = connection;

        try
        {
            // 连接建立即推送当前配置快照
            await SendAsync(connection, BuildConfigMessage());

            await ReceiveLoopAsync(connection, context.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.WriteLine($"集群节点 {node.Name} 同步通道异常：{ex.Message}");
        }
        finally
        {
            Connections.TryRemove(new KeyValuePair<string, ClusterConnection>(nodeId, connection));
            GatewayResourceMetrics.WebSocketClosed();
        }
    }

    private static async Task ReceiveLoopAsync(ClusterConnection connection, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        using var stream = new MemoryStream();

        while (connection.Socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            stream.SetLength(0);

            WebSocketReceiveResult result;
            do
            {
                result = await connection.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close) return;
                stream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            var message = ClusterProtocol.Decode(stream);
            if (message == null) continue;

            connection.LastSeen = DateTime.Now;

            switch (message.Type)
            {
                case ClusterMessage.TypePing:
                    await SendAsync(connection, new ClusterMessage { Type = ClusterMessage.TypePong });
                    break;
                case ClusterMessage.TypeAck:
                    connection.SyncedVersion = message.Version;
                    break;
            }
        }
    }

    /// <summary>配置变更后 500ms 去抖动推送（连续保存只推最后一次）</summary>
    private static void QueueBroadcast()
    {
        var state = _stateService?.Get();
        if (state == null || state.Role != ClusterRole.Master || Connections.IsEmpty) return;

        var cts = new CancellationTokenSource();
        var previous = Interlocked.Exchange(ref _debounceCts, cts);
        previous?.Cancel();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, cts.Token);
                await BroadcastAsync();
            }
            catch (OperationCanceledException)
            {
                // 被更新的变更取代
            }
        }, CancellationToken.None);
    }

    /// <summary>立即向所有在线节点推送配置快照</summary>
    public static async Task BroadcastAsync()
    {
        if (Connections.IsEmpty) return;

        var message = BuildConfigMessage();

        foreach (var connection in Connections.Values)
        {
            try
            {
                await SendAsync(connection, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"向节点 {connection.NodeId} 推送配置失败：{ex.Message}");
            }
        }
    }

    private static ClusterMessage BuildConfigMessage()
    {
        var config = _configService!.ExportSnapshot();
        var payload = new ClusterConfigPayload { Config = config };

        // 附带证书文件内容：从节点没有共享磁盘，PFX 随配置一起分发
        foreach (var cert in config.Certs)
        {
            var file = cert.Certs?.File;
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) continue;

            payload.CertFiles.Add(new ClusterCertFile
            {
                CertId = cert.Id,
                FileName = Path.GetFileName(file),
                Data = File.ReadAllBytes(file)
            });
        }

        return new ClusterMessage
        {
            Type = ClusterMessage.TypeConfig,
            Version = _configService.Version,
            Config = payload
        };
    }

    private static async Task SendAsync(ClusterConnection connection, ClusterMessage message)
    {
        var bytes = ClusterProtocol.Encode(message);

        await connection.SendLock.WaitAsync();
        try
        {
            await connection.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true,
                CancellationToken.None);
        }
        finally
        {
            connection.SendLock.Release();
        }
    }
}
