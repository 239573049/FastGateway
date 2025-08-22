using System.Text.Json;
using Yarp.ReverseProxy.Configuration;
using AppContext = TunnelClient.AppContext;

namespace TunnelClient.Model;

public class Tunnel
{
    public int Port { get; set; }

    /// <summary>
    ///     令牌
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    ///     ws 或 h2
    /// </summary>
    public string Type { get; set; }

    public bool ServerHttp2Support { get; set; }

    public bool IsHttp2 => Type.Equals("h2", StringComparison.OrdinalIgnoreCase);

    public bool IsWebSocket => Type.Equals("ws", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     服务器地址
    /// </summary>
    public string ServerUrl { get; set; }

    /// <summary>
    ///     名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    ///     传输协议类型
    /// </summary>
    public int ReconnectInterval { get; set; }

    /// <summary>
    ///     心跳间隔，单位秒
    /// </summary>
    public int HeartbeatInterval { get; set; }

    /// <summary>
    ///     代理配置
    /// </summary>
    public TunnelProxy[] Proxy { get; set; }

    public static Tunnel GetTunnel()
    {
        // 读取配置文件
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tunnel.json");

        var json = File.ReadAllText(path);

        var tunnel = JsonSerializer.Deserialize<Tunnel>(json, AppContext.JsonOptions);

        return tunnel;
    }

    public static (IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) ToYarpOption()
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        var tunnel = GetTunnel();

        foreach (var proxy in tunnel.Proxy)
        {
            var routeMetadata = new Dictionary<string, string>(0);
            var routeId = Guid.NewGuid().ToString("N");
            var clusterId = Guid.NewGuid().ToString("N");

            var path = proxy.Route ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path))
                path = "/{**catch-all}";
            else if (path == "/")
                path = "/{**catch-all}";
            else
                path = $"/{path.TrimStart('/')}/{{**catch-all}}";

            var route = new RouteConfig
            {
                RouteId = routeId,
                ClusterId = clusterId,
                Match = new RouteMatch
                {
                    Path = path,
                    Hosts = proxy.Domains
                },
                Metadata = routeMetadata
            };

            DestinationConfig config;

            if (!string.IsNullOrEmpty(proxy.Host))
                config = new DestinationConfig
                {
                    Address = proxy.LocalRemote,
                    Host = proxy.Host
                };
            else
                config = new DestinationConfig
                {
                    Address = proxy.LocalRemote
                };

            var cluster = new ClusterConfig
            {
                ClusterId = clusterId,
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    {
                        Guid.NewGuid().ToString("N"),
                        config
                    }
                }
            };

            clusters.Add(cluster);

            routes.Add(route);
        }

        return (routes, clusters);
    }

    public void Validate()
    {
        if (string.IsNullOrEmpty(Token))
            throw new ArgumentException("Token cannot be null or empty.");

        if (string.IsNullOrEmpty(ServerUrl))
            throw new ArgumentException("ServerUrl cannot be null or empty.");

        if (ReconnectInterval <= 0) ReconnectInterval = 5; // 默认重连间隔为5秒

        if (HeartbeatInterval <= 0) HeartbeatInterval = 30; // 默认心跳间隔为30秒

        if (Proxy == null || Proxy.Length == 0)
            throw new ArgumentException("At least one proxy configuration is required.");

        foreach (var proxy in Proxy)
        {
            if (string.IsNullOrEmpty(proxy.Route))
                throw new ArgumentException("Proxy Route cannot be null or empty.");

            if (string.IsNullOrEmpty(proxy.LocalRemote))
                throw new ArgumentException("Proxy LocalRemote cannot be null or empty.");

            if (!proxy.Route.StartsWith("/"))
                throw new ArgumentException("Proxy Route must start with a '/' character.");

            if (!proxy.LocalRemote.StartsWith("http://") && !proxy.LocalRemote.StartsWith("https://") &&
                !proxy.LocalRemote.StartsWith("ws://") && !proxy.LocalRemote.StartsWith("wss://"))
                throw new ArgumentException(
                    "Proxy LocalRemote must start with 'http://' or 'https://'. Or 'ws://' or 'wss://'.");
        }
    }

    public class TunnelProxy
    {
        /// <summary>
        ///     为空则默认
        /// </summary>
        public string? Host { get; set; }

        /// <summary>
        ///     域名匹配
        /// </summary>
        public string[] Domains { get; set; }

        /// <summary>
        ///     拦截网格路由
        /// </summary>
        public string Route { get; set; }

        /// <summary>
        ///     代理到本地服务
        /// </summary>
        public string LocalRemote { get; set; }

        /// <summary>
        ///     描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     是否启用
        /// </summary>
        public bool Enabled { get; set; }
    }
}