using System.Security.Cryptography;
using System.Text;
using Core.Entities;
using FastGateway.Options;
using FastGateway.Services;

namespace FastGateway.Tunnels;

/// <summary>
///     隧道节点持久化与认证入口。
///     网关子应用各自持有独立的 ConfigurationService 实例，若经由子应用实例写节点状态，
///     会用启动时的旧快照覆盖主实例的最新配置；因此节点相关读写统一走主应用实例（静态桥接）。
/// </summary>
public static class TunnelNodeStore
{
    private static ConfigurationService? _configService;

    public static void Initialize(ConfigurationService configService)
    {
        _configService = configService;
    }

    private static ConfigurationService ConfigService =>
        _configService ?? throw new InvalidOperationException("TunnelNodeStore 尚未初始化");

    public static List<TunnelNode> GetNodes() => ConfigService.GetTunnelNodes();

    public static TunnelNode? GetNode(string name) => ConfigService.GetTunnelNode(name);

    public static void AddNode(TunnelNode node) => ConfigService.AddTunnelNode(node);

    public static void UpdateNode(TunnelNode node) => ConfigService.UpdateTunnelNode(node);

    public static void DeleteNode(string name) => ConfigService.DeleteTunnelNode(name);

    public static Server? GetServer(string serverId) => ConfigService.GetServer(serverId);

    public static List<Server> GetServers() => ConfigService.GetServers();

    public static DomainName[] GetDomainNamesByServerId(string serverId) =>
        ConfigService.GetDomainNamesByServerId(serverId);

    /// <summary>
    ///     生成节点密钥：fgk_ 前缀 + 32 字节随机数（URL 安全 Base64）
    /// </summary>
    public static string GenerateNodeKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "fgk_" + Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <summary>
    ///     节点名合法性：用于内部主机名 node_{name} 与路由 Id，限制为字母数字与 - _
    /// </summary>
    public static bool IsValidNodeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 64) return false;
        foreach (var c in name)
            if (!char.IsAsciiLetterOrDigit(c) && c != '-' && c != '_')
                return false;
        return true;
    }

    /// <summary>
    ///     心跳间隔钳制到 [10, 300] 秒；未上报（0）时用默认 50 秒
    /// </summary>
    public static int ClampHeartbeatSeconds(int reported)
    {
        if (reported <= 0) return 50;
        return Math.Clamp(reported, 10, 300);
    }

    /// <summary>
    ///     代理规则稳定 Id：内容哈希，跨注册/重启保持一致，供覆盖配置与 YARP 路由 Id 复用
    /// </summary>
    public static string ComputeProxyId(string? host, string route, string[]? domains, string localRemote)
    {
        var raw = $"{host}|{route}|{string.Join(",", domains ?? [])}|{localRemote}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexStringLower(hash)[..16];
    }

    /// <summary>
    ///     从请求中提取节点凭据：优先 Authorization: Bearer，query/header 仅兼容旧客户端
    /// </summary>
    public static string? ExtractCredential(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorization))
        {
            const string bearerPrefix = "Bearer ";
            return authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
                ? authorization[bearerPrefix.Length..].Trim()
                : authorization.Trim();
        }

        var queryToken = context.Request.Query["token"].ToString();
        if (!string.IsNullOrEmpty(queryToken)) return queryToken;

        var headerToken = context.Request.Headers["token"].ToString();
        return string.IsNullOrEmpty(headerToken) ? null : headerToken;
    }

    /// <summary>
    ///     节点认证：
    ///     1) 节点存在且密钥匹配 → 通过；
    ///     2) 已配置全局 TunnelToken 且凭据匹配 → 兼容旧客户端：节点不存在时自动创建（标记自动注册），
    ///        已存在时仅限自动注册的节点；
    ///     3) 其余 → 拒绝。节点被禁用时返回 403。
    /// </summary>
    public static TunnelAuthResult Authenticate(string nodeName, string? credential)
    {
        if (string.IsNullOrEmpty(credential))
            return TunnelAuthResult.Fail(StatusCodes.Status401Unauthorized, "缺少节点凭据");

        var node = GetNode(nodeName);

        if (node != null && !string.IsNullOrEmpty(node.NodeKey) &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(credential), Encoding.UTF8.GetBytes(node.NodeKey)))
        {
            return node.Enabled
                ? TunnelAuthResult.Ok(node)
                : TunnelAuthResult.Fail(StatusCodes.Status403Forbidden, "节点已被禁用");
        }

        var globalToken = FastGatewayOptions.TunnelToken;
        if (globalToken != null && CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(credential), Encoding.UTF8.GetBytes(globalToken)))
        {
            if (node == null)
            {
                if (!IsValidNodeName(nodeName))
                    return TunnelAuthResult.Fail(StatusCodes.Status400BadRequest,
                        "节点名不合法（仅支持字母、数字、-、_，最长 64 字符）");

                node = new TunnelNode
                {
                    Name = nodeName,
                    Description = "通过全局 TunnelToken 自动注册",
                    NodeKey = GenerateNodeKey(),
                    Enabled = true,
                    AutoRegistered = true,
                    CreatedAt = DateTime.Now
                };
                AddNode(node);
                return TunnelAuthResult.Ok(node);
            }

            // 面板手动创建的节点只认自己的节点密钥，全局 Token 不能冒用其节点名
            if (!node.AutoRegistered)
                return TunnelAuthResult.Fail(StatusCodes.Status401Unauthorized, "节点凭据无效");

            return node.Enabled
                ? TunnelAuthResult.Ok(node)
                : TunnelAuthResult.Fail(StatusCodes.Status403Forbidden, "节点已被禁用");
        }

        return TunnelAuthResult.Fail(StatusCodes.Status401Unauthorized, "节点凭据无效");
    }
}

public readonly struct TunnelAuthResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string Message { get; init; }
    public TunnelNode? Node { get; init; }

    public static TunnelAuthResult Ok(TunnelNode node) => new()
        { Success = true, StatusCode = StatusCodes.Status200OK, Message = string.Empty, Node = node };

    public static TunnelAuthResult Fail(int statusCode, string message) => new()
        { Success = false, StatusCode = statusCode, Message = message };
}
