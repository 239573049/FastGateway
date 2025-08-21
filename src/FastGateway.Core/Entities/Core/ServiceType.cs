namespace FastGateway.Entities.Core;

public enum ServiceType
{
    /// <summary>
    /// 单服务
    /// </summary>
    Service = 0,
    
    /// <summary>
    /// 服务集群
    /// </summary>
    ServiceCluster = 1,
    
    /// <summary>
    /// 静态文件
    /// </summary>
    StaticFile = 2
}