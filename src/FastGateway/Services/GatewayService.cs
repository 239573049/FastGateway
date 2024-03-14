namespace FastGateway.Services;

public class GatewayService(
    IFreeSql freeSql,
    InMemoryConfigProvider inMemoryConfigProvider,
    StaticFileProxyService staticFileProxyService)
{
    public static List<RouteConfig> Routes { get; private set; } = [];

    public static List<ClusterConfig> Clusters { get; } = [];

    /// <summary>
    ///     初始化刷新路由配置
    /// </summary>
    public async Task RefreshConfig()
    {
        var routes = await freeSql.Select<RouteEntity>().ToListAsync();
        var clusters = await freeSql.Select<ClusterEntity>().ToListAsync();

        Clusters.Clear();
        Routes.Clear();

        // 路由支持多个集群
        Routes = routes.Select(route => new RouteConfig
        {
            RouteId = route.RouteId,
            ClusterId = route.ClusterId,
            MaxRequestBodySize = route.MaxRequestBodySize,
            AuthorizationPolicy = route.AuthorizationPolicy,
            Match = new RouteMatch
            {
                Path = route.Path,
                Hosts = route.Hosts
            }
        }).ToList();

        foreach (var cluster in from item in clusters
                 let items = item.DestinationsEntities.ToDictionary(entity => entity.Id,
                     entity => new DestinationConfig
                         { Address = entity.Address, Host = entity.Host, Health = entity.Health })
                 select new ClusterConfig
                 {
                     ClusterId = item.ClusterId,
                     Destinations = items
                 })
            Clusters.Add(cluster);

        inMemoryConfigProvider.Update(Routes, Clusters);

        // 刷新静态文件代理配置
        await staticFileProxyService.RefreshConfig();
    }

    /// <summary>
    ///     创建一个路由
    /// </summary>
    /// <param name="routeEntity"></param>
    /// <returns></returns>
    public async Task<ResultDto> CreateRouteAsync(RouteEntity routeEntity)
    {
        if (await freeSql.Select<RouteEntity>().AnyAsync(x => x.RouteId == routeEntity.RouteId))
            return ResultDto.Error("路由Id已存在");

        if (routeEntity.Hosts == null || routeEntity.Hosts.Length == 0)
        {
            // 如果没有Host则判断Path是否存在，如果Path存在则不允许创建
            if (await freeSql.Select<RouteEntity>()
                    .AnyAsync(x => x.Path == routeEntity.Path))
                return ResultDto.Error("路由Path已存在");
        }
        else
        {
            var routes = await freeSql.Select<RouteEntity>()
                .Where(x => x.Path == routeEntity.Path).ToListAsync();

            // 如果有Host则判断Path是否存在，如果Path存在则判断Host是否存在，如果Host存在则不允许创建
            if (routes?.Any(x => x.Hosts.Any(y => routeEntity?.Hosts?.Contains(y) == true)) == true)
                return ResultDto.Error("路由Host已存在");
        }

        routeEntity.RouteId = Guid.NewGuid().ToString("N");
        routeEntity.CreatedTime = DateTime.Now;

        await freeSql.Insert(routeEntity).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    ///     修改路由
    /// </summary>
    /// <param name="routeEntity"></param>
    /// <returns></returns>
    public async Task<ResultDto> UpdateRouteAsync(RouteEntity routeEntity)
    {
        var entity = await freeSql.Select<RouteEntity>().Where(x => x.RouteId == routeEntity.RouteId).FirstAsync();

        if (entity == null) return ResultDto.Error("路由Id不存在");

        if (routeEntity.Hosts == null || routeEntity.Hosts.Length == 0)
        {
            // 如果没有Host则判断Path是否存在，如果Path存在则不允许创建
            if (await freeSql.Select<RouteEntity>()
                    .AnyAsync(x => x.Path == routeEntity.Path && x.RouteId != routeEntity.RouteId))
                return ResultDto.Error("路由Path已存在");
        }
        else
        {
            var routes = await freeSql.Select<RouteEntity>()
                .Where(x => x.Path == routeEntity.Path && x.RouteId != routeEntity.RouteId).ToListAsync();

            // 如果有Host则判断Path是否存在，如果Path存在则判断Host是否存在，如果Host存在则不允许创建
            if (routes.Any(x => x.Hosts.Any(y => routeEntity.Hosts.Contains(y)))) return ResultDto.Error("路由Host已存在");
        }

        entity.RouteName = routeEntity.RouteName;
        entity.Description = routeEntity.Description;
        entity.ClusterId = routeEntity.ClusterId;
        entity.AuthorizationPolicy = routeEntity.AuthorizationPolicy;
        entity.RequireHttpsMetadata = routeEntity.RequireHttpsMetadata;
        entity.AuthorizationPolicyAddress = routeEntity.AuthorizationPolicyAddress;
        entity.MaxRequestBodySize = routeEntity.MaxRequestBodySize;
        entity.Path = routeEntity.Path;
        entity.Hosts = routeEntity.Hosts;

        await freeSql.Update<RouteEntity>().SetSource(entity).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    ///     删除路由
    /// </summary>
    /// <param name="routeId"></param>
    /// <returns></returns>
    public async Task<ResultDto> DeleteRouteAsync(string routeId)
    {
        if (!await freeSql.Select<RouteEntity>().AnyAsync(x => x.RouteId == routeId)) return ResultDto.Error("路由Id不存在");

        await freeSql.Delete<RouteEntity>().Where(x => x.RouteId == routeId).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    ///     获取路由
    /// </summary>
    /// <returns></returns>
    public async Task<ResultDto<List<RouteDto>>> GetRouteAsync()
    {
        var routeEntity = await freeSql.Select<RouteEntity>()
            .OrderByDescending(x => x.CreatedTime)
            .ToListAsync();
        if (routeEntity == null) return ResultDto<List<RouteDto>>.Error("路由Id不存在");

        var ids = routeEntity.Select(x => x.ClusterId).ToList();

        var clusterEntity = await freeSql.Select<ClusterEntity>().Where(x => ids.Contains(x.ClusterId)).ToListAsync();

        var result = routeEntity.Select(x => new RouteDto(x.RouteId, x.RouteName, x.Description, x.ClusterId,
            x.MaxRequestBodySize, x.Path, x.Hosts, null)).ToList();

        foreach (var entity in result)
            entity.ClusterEntity = clusterEntity.FirstOrDefault(x => x.ClusterId == entity.ClusterId);

        return ResultDto<List<RouteDto>>.Success(result.ToList());
    }

    /// <summary>
    ///     创建集群
    /// </summary>
    /// <param name="clusterEntity"></param>
    /// <returns></returns>
    public async Task<ResultDto> CreateClusterAsync(ClusterEntity clusterEntity)
    {
        if (await freeSql.Select<ClusterEntity>().AnyAsync(x => x.ClusterName == clusterEntity.ClusterName))
            return ResultDto.Error("集群Id已存在");

        clusterEntity.ClusterId = Guid.NewGuid().ToString("N");

        foreach (var destinationsEntity in clusterEntity.DestinationsEntities)
            destinationsEntity.Id = Guid.NewGuid().ToString("N");

        clusterEntity.CreatedTime = DateTime.Now;

        await freeSql.Insert(clusterEntity).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    ///     更新集群
    /// </summary>
    /// <param name="clusterEntity"></param>
    /// <returns></returns>
    public async Task<ResultDto> UpdateClusterAsync(ClusterEntity clusterEntity)
    {
        var entity = await freeSql.Select<ClusterEntity>().Where(x => x.ClusterId == clusterEntity.ClusterId)
            .FirstAsync();

        if (entity == null) return ResultDto.Error("集群Id不存在");

        entity.ClusterName = clusterEntity.ClusterName;
        entity.Description = clusterEntity.Description;
        entity.DestinationsEntities = clusterEntity.DestinationsEntities;

        await freeSql.Update<ClusterEntity>().SetSource(entity).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    ///     删除集群
    /// </summary>
    /// <param name="clusterId"></param>
    /// <returns></returns>
    public async Task<ResultDto> DeleteClusterAsync(string clusterId)
    {
        if (!await freeSql.Select<ClusterEntity>().AnyAsync(x => x.ClusterId == clusterId))
            return ResultDto.Error("集群Id不存在");

        await freeSql.Delete<ClusterEntity>().Where(x => x.ClusterId == clusterId).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    ///     获取集群
    /// </summary>
    /// <returns></returns>
    public async Task<ResultDto<List<ClusterEntity>>> GetClusterAsync()
    {
        var clusterEntity = await freeSql.Select<ClusterEntity>()
            .OrderByDescending(x => x.CreatedTime)
            .ToListAsync();
        return clusterEntity == null
            ? ResultDto<List<ClusterEntity>>.Error("集群Id不存在")
            : ResultDto<List<ClusterEntity>>.Success(clusterEntity);
    }
}

public static class GatewayExtension
{
    public static void MapGateway(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/gateway/refresh-config", async (GatewayService gatewayService) =>
                await gatewayService.RefreshConfig())
            .RequireAuthorization();

        app.MapGet("/api/gateway/routes", async (GatewayService gatewayService) =>
                await gatewayService.GetRouteAsync())
            .RequireAuthorization();

        app.MapPost("/api/gateway/routes", async (GatewayService gatewayService, RouteEntity routeEntity) =>
                await gatewayService.CreateRouteAsync(routeEntity))
            .RequireAuthorization();

        app.MapPut("/api/gateway/routes", async (GatewayService gatewayService, RouteEntity routeEntity) =>
                await gatewayService.UpdateRouteAsync(routeEntity))
            .RequireAuthorization();

        app.MapDelete("/api/gateway/routes/{routeId}", async (GatewayService gatewayService, string routeId) =>
                await gatewayService.DeleteRouteAsync(routeId))
            .RequireAuthorization();

        app.MapGet("/api/gateway/clusters", async (GatewayService gatewayService) =>
                await gatewayService.GetClusterAsync())
            .RequireAuthorization();

        app.MapPost("/api/gateway/clusters", async (GatewayService gatewayService, ClusterEntity clusterEntity) =>
                await gatewayService.CreateClusterAsync(clusterEntity))
            .RequireAuthorization();

        app.MapPut("/api/gateway/clusters", async (GatewayService gatewayService, ClusterEntity clusterEntity) =>
                await gatewayService.UpdateClusterAsync(clusterEntity))
            .RequireAuthorization();

        app.MapDelete("/api/gateway/clusters/{clusterId}", async (GatewayService gatewayService, string clusterId) =>
                await gatewayService.DeleteClusterAsync(clusterId))
            .RequireAuthorization();
    }
}