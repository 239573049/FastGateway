namespace FastGateway.Core;

public enum ApiServiceType : byte
{
    /// <summary>
    /// 静态代理
    /// </summary>
    StaticProxy = 1,

    /// <summary>
    /// 单个服务
    /// </summary>
    SingleService = 2,

    /// <summary>
    /// 负载均衡
    /// </summary>
    LoadBalance = 3,
}