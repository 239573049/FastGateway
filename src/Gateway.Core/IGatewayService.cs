using Gateway.Core.Entities;
using Gateway.Dto;

namespace Gateway.Core;

public interface IGatewayService
{
    /// <summary>
    /// 刷新配置信息
    /// </summary>
    /// <returns></returns>
    Task RefreshConfig();
    
    /// <summary>
    /// 创建路由
    /// </summary>
    /// <param name="routeEntity"></param>
    /// <returns></returns>
    Task<ResultDto> CreateRouteAsync(RouteEntity routeEntity);
    
    /// <summary>
    /// 编辑路由
    /// </summary>
    /// <param name="routeEntity"></param>
    /// <returns></returns>
    Task<ResultDto> UpdateRouteAsync(RouteEntity routeEntity);
    
    /// <summary>
    /// 删除路由
    /// </summary>
    /// <param name="routeId"></param>
    /// <returns></returns>
    Task<ResultDto> DeleteRouteAsync(string routeId);
    
    /// <summary>
    /// 获取路由
    /// </summary>
    /// <returns></returns>
    Task<ResultDto<List<RouteDto>>> GetRouteAsync();

    /// <summary>
    /// 创建集群
    /// </summary>
    /// <param name="clusterEntity"></param>
    /// <returns></returns>
    Task<ResultDto> CreateClusterAsync(ClusterEntity clusterEntity);

    /// <summary>
    /// 编辑集群
    /// </summary>
    /// <param name="clusterEntity"></param>
    /// <returns></returns>
    Task<ResultDto> UpdateClusterAsync(ClusterEntity clusterEntity);
    
    /// <summary>
    /// 删除集群
    /// </summary>
    /// <param name="clusterId"></param>
    /// <returns></returns>
    Task<ResultDto> DeleteClusterAsync(string clusterId);
    
    /// <summary>
    /// 获取集群列表
    /// </summary>
    /// <returns></returns>
    Task<ResultDto<List<ClusterEntity>>> GetClusterAsync();
}