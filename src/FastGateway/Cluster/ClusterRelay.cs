using Core.Entities;

namespace FastGateway.Cluster;

/// <summary>
///     路由级「访问节点」中继决策：
///     - Worker 收到非本节点路由 → 回源到主网关业务端口（主网关地址天然可达）；
///     - Master 收到从节点路由 → 经集群隧道（node_cluster-*）转给目标从节点；
///     - Worker 收到其他 Worker 的路由 → 先回源 Master，由 Master 级联进隧道（两跳）。
///     身份快照由 ClusterStateService 在每次状态变更时推送，读路径零锁零分配。
/// </summary>
public static class ClusterRelay
{
    /// <summary>AccessNodeId 中表示主网关的固定值</summary>
    public const string MasterNodeId = "master";

    /// <summary>中继跳数头：进入节点时为已经过的中继次数</summary>
    public const string RelayCountHeader = "X-FastGateway-Relay";

    /// <summary>中继令牌头：下一跳节点校验来源是否为集群成员</summary>
    public const string RelayTokenHeader = "X-FastGateway-Relay-Token";

    /// <summary>Cluster 元数据键，值为 AccessNodeId，标记该 YARP cluster 为中继目的地</summary>
    public const string RelayMetadataKey = "FastGateway.Relay";

    /// <summary>正常链路最多两跳（Worker → Master → Worker），超出即视为环路</summary>
    public const int MaxHops = 2;

    private static volatile Snapshot _snapshot = new(ClusterRole.Standalone, null, null, null,
        new Dictionary<string, string>(), new HashSet<string>());

    /// <summary>由 ClusterStateService 在锁内调用，state 为内部实例，仅读取标量并拷贝集合</summary>
    public static void UpdateSnapshot(ClusterState state)
    {
        string? masterHost = null;
        if (!string.IsNullOrEmpty(state.MasterEndpoint) &&
            Uri.TryCreate(state.MasterEndpoint, UriKind.Absolute, out var uri))
            masterHost = uri.Host;

        var nodeTokens = new Dictionary<string, string>(state.Nodes.Count);
        var tokenSet = new HashSet<string>(state.Nodes.Count);
        foreach (var node in state.Nodes)
        {
            nodeTokens[node.Id] = node.Token;
            tokenSet.Add(node.Token);
        }

        _snapshot = new Snapshot(state.Role, state.NodeId, state.NodeToken, masterHost, nodeTokens, tokenSet);
    }

    /// <summary>
    ///     解析路由的中继目的地址；返回 null 表示本节点直连上游（默认行为）。
    /// </summary>
    public static string? ResolveRelayAddress(string? accessNodeId, Server server)
    {
        if (string.IsNullOrWhiteSpace(accessNodeId)) return null;

        var snapshot = _snapshot;
        if (snapshot.Role == ClusterRole.Standalone) return null;

        if (accessNodeId == MasterNodeId)
        {
            if (snapshot.Role == ClusterRole.Master) return null;
            return BuildMasterDataAddress(snapshot, server);
        }

        if (snapshot.Role == ClusterRole.Worker)
        {
            if (accessNodeId == snapshot.NodeId) return null;

            // 目标是其他 Worker：回源 Master，由 Master 级联进隧道
            return BuildMasterDataAddress(snapshot, server);
        }

        // Master：目标从节点已被移除时退回直连，避免路由悬空
        if (!snapshot.NodeTokens.ContainsKey(accessNodeId)) return null;

        return $"{RelayScheme(server)}://node_{TunnelNodeName(accessNodeId, RelayPort(server))}";
    }

    /// <summary>集群隧道注册名：cluster-{nodeId}-{port}，目的地即 node_ 前缀 + 该名称</summary>
    public static string TunnelNodeName(string nodeId, int port)
    {
        return $"cluster-{nodeId}-{port}";
    }

    /// <summary>
    ///     业务数据面实际端口：仅「80 + HTTPS」组合在 443 上提供 TLS，其余端口按明文处理
    ///     （与 BuilderGateway 的 Kestrel 监听行为一致）
    /// </summary>
    public static int RelayPort(Server server)
    {
        return server is { IsHttps: true, Listen: 80 } ? 443 : server.Listen;
    }

    private static string RelayScheme(Server server)
    {
        return server is { IsHttps: true, Listen: 80 } ? "https" : "http";
    }

    private static string? BuildMasterDataAddress(Snapshot snapshot, Server server)
    {
        if (string.IsNullOrEmpty(snapshot.MasterHost)) return null;
        return $"{RelayScheme(server)}://{snapshot.MasterHost}:{RelayPort(server)}";
    }

    /// <summary>出方向：本节点向下一跳出示的令牌</summary>
    public static string? GetRelayToken(string accessNodeId)
    {
        var snapshot = _snapshot;
        return snapshot.Role switch
        {
            // Worker 的所有中继都发往 Master，以自己的 NodeToken 表明身份
            ClusterRole.Worker => snapshot.NodeToken,
            // Master 中继进隧道时出示目标节点的令牌（目标节点仅信任自己的令牌）
            ClusterRole.Master => snapshot.NodeTokens.GetValueOrDefault(accessNodeId),
            _ => null
        };
    }

    /// <summary>入方向：校验中继请求的令牌是否属于集群成员</summary>
    public static bool ValidateRelayToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return false;

        var snapshot = _snapshot;
        return snapshot.Role switch
        {
            ClusterRole.Master => snapshot.TokenSet.Contains(token),
            ClusterRole.Worker => ClusterStateService.TokenEquals(snapshot.NodeToken, token),
            _ => false
        };
    }

    private sealed record Snapshot(
        ClusterRole Role,
        string? NodeId,
        string? NodeToken,
        string? MasterHost,
        Dictionary<string, string> NodeTokens,
        HashSet<string> TokenSet);
}
