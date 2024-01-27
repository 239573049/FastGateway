namespace Gateway.Services;

public class StaticFileProxyService(IFreeSql freeSql)
{
    public static IReadOnlyList<StaticFileProxyEntity> StaticFileProxyEntityList { get; private set; } = [];

    /// <summary>
    /// 刷新配置缓存
    /// </summary>
    public async Task RefreshConfig()
    {
        var staticFileProxyEntities = await freeSql.Select<StaticFileProxyEntity>().ToListAsync();

        StaticFileProxyEntityList = staticFileProxyEntities;
    }

    /// <summary>
    /// 创建静态文件代理配置
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    public async Task<ResultDto> CreateAsync(StaticFileProxyEntity entity)
    {
        entity.Hosts = entity.Hosts.Where(x => !string.IsNullOrEmpty(x)).ToArray();
        if (entity.Hosts.Length != 0)
        {
            var entities = await freeSql.Select<StaticFileProxyEntity>()
                .Where(x => x.Path == entity.Path && !string.IsNullOrEmpty(x.Hosts.ToString()))
                .ToListAsync();

            if (entities.Any(x => x.Hosts.Any(h => entity.Hosts.Any(e => e == h))))
            {
                return ResultDto.Error("已存在相同的域名和路径");
            }
        }
        else
        {
            if (await freeSql.Select<StaticFileProxyEntity>().AnyAsync(x => x.Path == entity.Path))
            {
                return ResultDto.Error("已存在相同的路径");
            }
        }

        entity.Id = Guid.NewGuid().ToString("N");
        entity.CreatedTime = DateTime.Now;

        await freeSql.Insert(entity).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    /// 修改静态文件代理配置
    /// </summary>
    /// <param name="entity"></param>
    /// <returns></returns>
    public async Task<ResultDto> UpdateAsync(StaticFileProxyEntity entity)
    {
        var value = await freeSql.Select<StaticFileProxyEntity>().Where(x => x.Id == entity.Id).FirstAsync();

        if (value == null)
        {
            return ResultDto.Error("不存在");
        }

        // 先匹配域名数组，如果存在相同域名的，再匹配路径，如果都存在，则不允许创建
        entity.Hosts = entity.Hosts.Where(x => !string.IsNullOrEmpty(x)).ToArray();
        if (entity.Hosts.Length != 0)
        {
            var entities = await freeSql.Select<StaticFileProxyEntity>()
                .Where(x => x.Id != entity.Id && x.Path == entity.Path && !string.IsNullOrEmpty(x.Hosts.ToString()))
                .ToListAsync();

            if (entities.Any(x => x.Hosts.Any(h => entity.Hosts.Any(e => e == h))))
            {
                return ResultDto.Error("已存在相同的域名和路径");
            }
        }
        else
        {
            if (await freeSql.Select<StaticFileProxyEntity>().AnyAsync(x => x.Id != entity.Id && x.Path == entity.Path))
            {
                return ResultDto.Error("已存在相同的路径");
            }
        }

        value.Description = entity.Description;
        value.GZip = entity.GZip;
        value.Hosts = entity.Hosts;
        value.Index = entity.Index;
        value.Name = entity.Name;
        value.ResponseHeaders = entity.ResponseHeaders;
        value.Path = entity.Path;
        value.TryFiles = entity.TryFiles;
        value.Root = entity.Root;

        await freeSql.Update<StaticFileProxyEntity>().SetSource(value).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    /// 删除静态文件代理配置
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public async Task<ResultDto> DeleteAsync(string id)
    {
        if (!await freeSql.Select<StaticFileProxyEntity>().AnyAsync(x => x.Id == id))
        {
            return ResultDto.Error("不存在");
        }

        await freeSql.Delete<StaticFileProxyEntity>().Where(x => x.Id == id).ExecuteAffrowsAsync();

        return ResultDto.Success();
    }

    /// <summary>
    /// 获取静态文件代理配置
    /// </summary>
    /// <param name="keyword"></param>
    /// <returns></returns>
    public async Task<PageResultDto<StaticFileProxyEntity>> GetListAsync(string keyword)
    {
        var query = freeSql.Select<StaticFileProxyEntity>()
            .WhereIf(!string.IsNullOrEmpty(keyword), x => x.Hosts.Contains(keyword) || x.Path.Contains(keyword));

        var total = await query.CountAsync();

        var list = await query
            .OrderByDescending(x => x.CreatedTime)
            .ToListAsync();

        return new PageResultDto<StaticFileProxyEntity>(total, list);
    }
}

public static class StaticFileProxyExtension
{
    public static void MapStaticFileProxy(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gateway/static-file-proxy", async (StaticFileProxyService staticFileProxyService,
                string keyword) =>
            await staticFileProxyService.GetListAsync(keyword));

        app.MapPost("/api/gateway/static-file-proxy",
            async (StaticFileProxyService staticFileProxyService, StaticFileProxyEntity entity) =>
                await staticFileProxyService.CreateAsync(entity));

        app.MapPut("/api/gateway/static-file-proxy",
            async (StaticFileProxyService staticFileProxyService, StaticFileProxyEntity entity) =>
                await staticFileProxyService.UpdateAsync(entity));

        app.MapDelete("/api/gateway/static-file-proxy",
            async (StaticFileProxyService staticFileProxyService, string id) =>
                await staticFileProxyService.DeleteAsync(id));
    }
}