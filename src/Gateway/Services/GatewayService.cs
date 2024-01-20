namespace Gateway.Services;

public class GatewayService(IFreeSql freeSql, InMemoryConfigProvider inMemoryConfigProvider)
{
    public static List<RouteConfig> Routes { get; private set; } = [];

    public static List<ClusterConfig> Clusters { get; private set; } = [];

    /// <summary>
    /// 初始化刷新路由配置
    /// </summary>
    public async Task RefreshConfig()
    {
        var routes = await freeSql.Select<RouteEntity>().ToListAsync();
        var clusters = await freeSql.Select<ClusterEntity>().ToListAsync();
        
        Clusters.Clear();
        Routes.Clear();
        
        Routes = routes.Select(route => new RouteConfig
        {
            RouteId = route.RouteId,
            ClusterId = route.ClusterId,
            MaxRequestBodySize = route.MaxRequestBodySize,
            Match = new RouteMatch()
            {
                Path = route.MatchEntities.Path
            }
        }).ToList();

        foreach (var item in clusters)
        {
            var items = item.DestinationsEntities.ToDictionary(entity => entity.Id, entity => new DestinationConfig() { Address = entity.Address, Host = entity.Host, });

            var cluster = new ClusterConfig()
            {
                ClusterId = item.ClusterId,
                Destinations = items
            };
            
            Clusters.Add(cluster);
        }

        inMemoryConfigProvider.Update(Routes, Clusters);
    }

    /// <summary>
    /// 创建一个路由
    /// </summary>
    /// <param name="routeEntity"></param>
    /// <returns></returns>
    public async Task<ResultDto> CreateRouteAsync(RouteEntity routeEntity)
    {
        if (await freeSql.Select<RouteEntity>().AnyAsync(x => x.RouteId == routeEntity.RouteId))
        {
            return ResultDto.Error("路由Id已存在");
        }
        
        routeEntity.RouteId = Guid.NewGuid().ToString("N");

        await freeSql.Insert(routeEntity).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    /// 修改路由
    /// </summary>
    /// <param name="routeEntity"></param>
    /// <returns></returns>
    public async Task<ResultDto> UpdateRouteAsync(RouteEntity routeEntity)
    {
        if (!await freeSql.Select<RouteEntity>().AnyAsync(x => x.RouteId == routeEntity.RouteId))
        {
            return ResultDto.Error("路由Id不存在");
        }

        await freeSql.Update<RouteEntity>().SetSource(routeEntity).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    /// 删除路由
    /// </summary>
    /// <param name="routeId"></param>
    /// <returns></returns>
    public async Task<ResultDto> DeleteRouteAsync(string routeId)
    {
        if (!await freeSql.Select<RouteEntity>().AnyAsync(x => x.RouteId == routeId))
        {
            return ResultDto.Error("路由Id不存在");
        }

        await freeSql.Delete<RouteEntity>().Where(x => x.RouteId == routeId).ExecuteAffrowsAsync();
        
        return ResultDto.Success();
    }

    /// <summary>
    /// 获取路由
    /// </summary>
    /// <returns></returns>
    public async Task<ResultDto<List<RouteDto>>> GetRouteAsync()
    {
        var routeEntity = await freeSql.Select<RouteEntity>().ToListAsync();
        if (routeEntity == null)
        {
            return ResultDto<List<RouteDto>>.Error("路由Id不存在");
        }
        
        var ids = routeEntity.Select(x => x.ClusterId).ToList();
        
        var clusterEntity = await freeSql.Select<ClusterEntity>().Where(x => ids.Contains(x.ClusterId)).ToListAsync();

        var result = routeEntity.Select(x => new RouteDto(x.RouteId, x.RouteName, x.Description, x.ClusterId,
            x.MaxRequestBodySize, new RouteMatchDto()
            {
                Path = x.MatchEntities.Path,
                Hosts = x.MatchEntities.Hosts
            }, null)).ToList();
        
        foreach (var entity in result)
        {
            entity.ClusterEntity = clusterEntity.FirstOrDefault(x => x.ClusterId == entity.ClusterId);
        }

        return ResultDto<List<RouteDto>>.Success(result.ToList());
    }

    /// <summary>
    /// 创建集群
    /// </summary>
    /// <param name="clusterEntity"></param>
    /// <returns></returns>
    public async Task<ResultDto> CreateClusterAsync(ClusterEntity clusterEntity)
    {
        if (await freeSql.Select<ClusterEntity>().AnyAsync(x => x.ClusterName == clusterEntity.ClusterName))
        {
            return ResultDto.Error("集群Id已存在");
        }
        
        clusterEntity.ClusterId = Guid.NewGuid().ToString("N");

        foreach (var destinationsEntity in clusterEntity.DestinationsEntities)
        {
            destinationsEntity.Id = Guid.NewGuid().ToString("N");
        }

        await freeSql.Insert(clusterEntity).ExecuteAffrowsAsync();
        
        return ResultDto.Success();
    }

    /// <summary>
    /// 更新集群
    /// </summary>
    /// <param name="clusterEntity"></param>
    /// <returns></returns>
    public async Task<ResultDto> UpdateClusterAsync(ClusterEntity clusterEntity)
    {
        if (!await freeSql.Select<ClusterEntity>().AnyAsync(x => x.ClusterId == clusterEntity.ClusterId))
        {
            return ResultDto.Error("集群Id不存在");
        }

        await freeSql.Update<ClusterEntity>().SetSource(clusterEntity).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    /// 删除集群
    /// </summary>
    /// <param name="clusterId"></param>
    /// <returns></returns>
    public async Task<ResultDto> DeleteClusterAsync(string clusterId)
    {
        if (!await freeSql.Select<ClusterEntity>().AnyAsync(x => x.ClusterId == clusterId))
        {
            return ResultDto.Error("集群Id不存在");
        }

        await freeSql.Delete<ClusterEntity>().Where(x => x.ClusterId == clusterId).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    /// 获取集群
    /// </summary>
    /// <returns></returns>
    public async Task<ResultDto<List<ClusterEntity>>> GetClusterAsync()
    {
        var clusterEntity = await freeSql.Select<ClusterEntity>().ToListAsync();
        if (clusterEntity == null)
        {
            return ResultDto<List<ClusterEntity>>.Error("集群Id不存在");
        }

        return ResultDto<List<ClusterEntity>>.Success(clusterEntity);
    }
}