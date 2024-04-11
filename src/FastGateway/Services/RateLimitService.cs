namespace FastGateway.Services;

public class RateLimitService
{
    public static async Task<ResultDto> CreateAsync(IFreeSql freeSql, RateLimit rateLimit)
    {
        if (await freeSql.Select<RateLimit>().AnyAsync(x => x.Name == rateLimit.Name))
        {
            return ResultDto.ErrorResult("限流策略名称已存在");
        }

        await freeSql.Insert(rateLimit).ExecuteAffrowsAsync();

        return ResultDto.SuccessResult();
    }

    public static async Task<ResultDto> UpdateAsync(IFreeSql freeSql, string name, RateLimit rateLimit)
    {
        await freeSql.Update<RateLimit>()
            .Where(x => x.Name == name)
            .Set(x => x.Enable, rateLimit.Enable)
            .Set(x => x.GeneralRules, rateLimit.GeneralRules)
            .Set(x => x.EndpointWhitelist, rateLimit.EndpointWhitelist)
            .Set(x => x.ClientIdHeader, rateLimit.ClientIdHeader)
            .Set(x => x.ClientWhitelist, rateLimit.ClientWhitelist)
            .Set(x => x.RealIpHeader, rateLimit.RealIpHeader)
            .Set(x => x.IpWhitelist, rateLimit.IpWhitelist)
            .Set(x => x.HttpStatusCode, rateLimit.HttpStatusCode)
            .Set(x => x.QuotaExceededMessage, rateLimit.QuotaExceededMessage)
            .Set(x=>x.RateLimitContentType,rateLimit.RateLimitContentType)
            .Set(x => x.RateLimitCounterPrefix, rateLimit.RateLimitCounterPrefix)
            .Set(x=>x.GeneralRules,rateLimit.GeneralRules)
            .ExecuteAffrowsAsync();


        return ResultDto.SuccessResult();
    }

    public static async Task<ResultDto> DeleteAsync(IFreeSql freeSql, string name)
    {
        await freeSql.Delete<RateLimit>()
            .Where(x => x.Name == name)
            .ExecuteAffrowsAsync();

        return ResultDto.SuccessResult();
    }

    public static async Task<PageResultDto<RateLimit>> GetListAsync(IFreeSql freeSql, int page, int pageSize)
    {
        var items = await freeSql.Select<RateLimit>()
            .Count(out var total)
            .Page(page, pageSize)
            .ToListAsync();

        return new PageResultDto<RateLimit>(total: total, items: items);
    }
    
    public static async Task<List<string>> GetNamesAsync(IFreeSql freeSql)
    {
        return await freeSql.Select<RateLimit>().ToListAsync(x => x.Name);
    }
}