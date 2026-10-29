using Core;
using FastGateway.Tunnels;
using Microsoft.Extensions.Logging.Abstractions;

namespace FastGateway.Cluster;

/// <summary>
///     主网关侧的集群数据面隧道枢纽。
///     从节点以 cluster-{nodeId}-{port} 为名向管理端 /internal/cluster/tunnel 出站注册（NodeToken 鉴权），
///     各网关子应用的 YARP 在转发 node_cluster-* 目的地时经此处的静态实例取隧道流 ——
///     必须是进程级单例：主连接挂在管理应用、转发发生在各子应用，二者 DI 容器互不相通。
/// </summary>
public static class ClusterTunnelHub
{
    public const string EndpointPath = "/internal/cluster/tunnel";

    public static readonly AgentClientManager Clients = new(new AgentStateChannel());

    public static readonly AgentTunnelFactory Tunnels = new(NullLogger<AgentTunnelFactory>.Instance);

    /// <summary>
    ///     管理应用上的隧道端点：无 tunnelId 为节点主连接（下发隧道指令），带 tunnelId 为数据回连。
    /// </summary>
    public static async Task HandleAsync(HttpContext context)
    {
        var feature = new FastFeature(context);
        if (!feature.IsRequest)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        var logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ClusterTunnelHub");

        // 数据回连：绑定到等待中的隧道
        if (Guid.TryParse(context.Request.Query["tunnelId"].ToString(), out var tunnelId))
        {
            if (!Tunnels.Contains(tunnelId))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            var tunnelStream = await feature.AcceptAsStreamAsync();
            var httpTunnel = new HttpTunnel(tunnelStream, tunnelId, feature.Protocol, logger);

            if (Tunnels.SetResult(httpTunnel))
            {
                try { await httpTunnel.Closed; }
                finally { await httpTunnel.DisposeAsync(); }
            }
            else
            {
                await httpTunnel.DisposeAsync();
            }

            return;
        }

        // 节点主连接：校验角色与 NodeToken
        var nodeName = context.Request.Query["nodeName"].ToString();
        var token = context.Request.Query["token"].ToString();

        if (!ValidateNode(context, nodeName, token))
        {
            // 与 TunnelClient 主连接一致：延迟响应减缓令牌爆破
            await Task.Delay(3000);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // 与既有隧道体系一致的目的地命名：node_ 前缀 + 注册名
        var host = "node_" + nodeName;

        try
        {
            var stream = await feature.AcceptAsSafeWriteStreamAsync();
            var connection = new AgentClientConnection(host, stream, new ConnectionConfig(), logger);

            await using var client = new AgentClient(connection, Tunnels, context);
            var added = await Clients.AddAsync(client, CancellationToken.None);
            try
            {
                if (added)
                {
                    logger.LogInformation("集群隧道节点已注册：{Host}", host);
                    await connection.WaitForCloseAsync();
                }
            }
            finally
            {
                if (added)
                {
                    try { await Clients.RemoveAsync(client, CancellationToken.None); }
                    catch { /* ignored */ }
                    logger.LogInformation("集群隧道节点已断开：{Host}", host);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "集群隧道主连接异常：{Host}", host);
        }
    }

    private static bool ValidateNode(HttpContext context, string nodeName, string token)
    {
        if (string.IsNullOrEmpty(nodeName) || string.IsNullOrEmpty(token)) return false;
        if (!nodeName.StartsWith("cluster-", StringComparison.Ordinal)) return false;

        var stateService = context.RequestServices.GetRequiredService<ClusterStateService>();
        var state = stateService.Get();
        if (state.Role != ClusterRole.Master) return false;

        // 注册名 cluster-{nodeId}-{port}：nodeId 为 GUID 含连字符，从尾部截掉端口段还原
        var lastDash = nodeName.LastIndexOf('-');
        if (lastDash <= "cluster-".Length) return false;

        var nodeId = nodeName["cluster-".Length..lastDash];
        var node = state.Nodes.FirstOrDefault(n => n.Id == nodeId);

        return node != null && ClusterStateService.TokenEquals(node.Token, token);
    }
}
