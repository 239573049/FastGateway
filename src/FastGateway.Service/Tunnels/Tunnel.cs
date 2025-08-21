namespace FastGateway.Service.Tunnels;

public class Tunnel
{
    /// <summary>
    /// 令牌
    /// </summary>
    public string Token { get; set; }

    /// <summary>
    /// 服务器地址
    /// </summary>
    public string ServerUrl { get; set; }

    /// <summary>
    /// 名称
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// 传输协议类型
    /// </summary>
    public int ReconnectInterval { get; set; }

    /// <summary>
    /// 心跳间隔，单位秒
    /// </summary>
    public int HeartbeatInterval { get; set; }

    /// <summary>
    /// 代理配置
    /// </summary>
    public TunnelProxy[] Proxy { get; set; }
    
    public class TunnelProxy
    {
        /// <summary>
        /// 为空则默认
        /// </summary>
        public string? Host { get; set; }

        /// <summary>
        /// 拦截网格路由
        /// </summary>
        public string Route { get; set; }

        /// <summary>
        /// 代理到本地服务
        /// </summary>
        public string LocalRemote { get; set; }

        /// <summary>
        /// 描述
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool Enabled { get; set; }
    }
}