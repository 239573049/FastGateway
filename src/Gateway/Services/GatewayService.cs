namespace Gateway.Services;

public class GatewayService
{
    public static RouteConfig[] Routes { get; private set; } = Array.Empty<RouteConfig>();

    public static ClusterConfig[] Clusters { get; private set; } = Array.Empty<ClusterConfig>();

    private readonly IFreeSql _freeSql;
    private readonly InMemoryConfigProvider _inMemoryConfigProvider;

    public GatewayService(IFreeSql freeSql, InMemoryConfigProvider inMemoryConfigProvider)
    {
        _freeSql = freeSql;
        _inMemoryConfigProvider = inMemoryConfigProvider;
        Task.Run(UpdateConfig);
    }

    /// <summary>
    /// 初始化刷新路由配置
    /// </summary>
    private async Task UpdateConfig()
    {
        var routes = await _freeSql.Select<RouteEntity>().ToListAsync();
        var clusters = await _freeSql.Select<ClusterEntity>().ToListAsync();
        Routes = routes.Select(route => new RouteConfig
        {
            RouteId = route.RouteId,
            ClusterId = route.ClusterId,
            MaxRequestBodySize = route.MaxRequestBodySize,
            Match = new RouteMatch()
            {
                Path = route.MatchEntities.Path
            }
        }).ToArray();

        Clusters = clusters.Select(cluster => new ClusterConfig
        {
            ClusterId = cluster.ClusterId,
            Destinations = cluster.DestinationsEntities.ToDictionary(destination => destination.Id, destination =>
                new DestinationConfig
                {
                    Address = destination.Address,
                    Host = destination.Host
                })
        }).ToArray();

        _inMemoryConfigProvider.Update(Routes, Clusters);
    }

    /// <summary>
    /// 创建一个路由
    /// </summary>
    /// <param name="routeEntity"></param>
    /// <returns></returns>
    public async Task<ResultDto> CreateRouteAsync(RouteEntity routeEntity)
    {
        if (await _freeSql.Select<RouteEntity>().AnyAsync(x => x.RouteId == routeEntity.RouteId))
        {
            return ResultDto.Error("路由Id已存在");
        }

        await _freeSql.Insert(routeEntity).ExecuteAffrowsAsync();
        await UpdateConfig();

        return ResultDto.Success();
    }

    /// <summary>
    /// 修改路由
    /// </summary>
    /// <param name="routeEntity"></param>
    /// <returns></returns>
    public async Task<ResultDto> UpdateRouteAsync(RouteEntity routeEntity)
    {
        if (!await _freeSql.Select<RouteEntity>().AnyAsync(x => x.RouteId == routeEntity.RouteId))
        {
            return ResultDto.Error("路由Id不存在");
        }

        await _freeSql.Update<RouteEntity>().SetSource(routeEntity).ExecuteAffrowsAsync();
        await UpdateConfig();

        return ResultDto.Success();
    }

    /// <summary>
    /// 删除路由
    /// </summary>
    /// <param name="routeId"></param>
    /// <returns></returns>
    public async Task<ResultDto> DeleteRouteAsync(string routeId)
    {
        if (!await _freeSql.Select<RouteEntity>().AnyAsync(x => x.RouteId == routeId))
        {
            return ResultDto.Error("路由Id不存在");
        }

        await _freeSql.Delete<RouteEntity>().Where(x => x.RouteId == routeId).ExecuteAffrowsAsync();
        await UpdateConfig();

        return ResultDto.Success();
    }

    /// <summary>
    /// 获取路由
    /// </summary>
    /// <returns></returns>
    public async Task<ResultDto<List<RouteEntity>>> GetRouteAsync()
    {
        var routeEntity = await _freeSql.Select<RouteEntity>().ToListAsync();
        if (routeEntity == null)
        {
            return ResultDto<List<RouteEntity>>.Error("路由Id不存在");
        }

        return ResultDto<List<RouteEntity>>.Success(routeEntity);
    }
    
    /// <summary>
    /// 创建集群
    /// </summary>
    /// <param name="clusterEntity"></param>
    /// <returns></returns>
    public async Task<ResultDto> CreateClusterAsync(ClusterEntity clusterEntity)
    {
        if (await _freeSql.Select<ClusterEntity>().AnyAsync(x => x.ClusterId == clusterEntity.ClusterId))
        {
            return ResultDto.Error("集群Id已存在");
        }

        await _freeSql.Insert(clusterEntity).ExecuteAffrowsAsync();
        await UpdateConfig();

        return ResultDto.Success();
    }
    
    /// <summary>
    /// 更新集群
    /// </summary>
    /// <param name="clusterEntity"></param>
    /// <returns></returns>
    public async Task<ResultDto> UpdateClusterAsync(ClusterEntity clusterEntity)
    {
        if (!await _freeSql.Select<ClusterEntity>().AnyAsync(x => x.ClusterId == clusterEntity.ClusterId))
        {
            return ResultDto.Error("集群Id不存在");
        }

        await _freeSql.Update<ClusterEntity>().SetSource(clusterEntity).ExecuteAffrowsAsync();
        await UpdateConfig();

        return ResultDto.Success();
    }
    
    /// <summary>
    /// 删除集群
    /// </summary>
    /// <param name="clusterId"></param>
    /// <returns></returns>
    public async Task<ResultDto> DeleteClusterAsync(string clusterId)
    {
        if (!await _freeSql.Select<ClusterEntity>().AnyAsync(x => x.ClusterId == clusterId))
        {
            return ResultDto.Error("集群Id不存在");
        }

        await _freeSql.Delete<ClusterEntity>().Where(x => x.ClusterId == clusterId).ExecuteAffrowsAsync();
        await UpdateConfig();

        return ResultDto.Success();
    }
    
    /// <summary>
    /// 获取集群
    /// </summary>
    /// <returns></returns>
    public async Task<ResultDto<List<ClusterEntity>>> GetClusterAsync()
    {
        var clusterEntity = await _freeSql.Select<ClusterEntity>().ToListAsync();
        if (clusterEntity == null)
        {
            return ResultDto<List<ClusterEntity>>.Error("集群Id不存在");
        }

        return ResultDto<List<ClusterEntity>>.Success(clusterEntity);
    }
    
}