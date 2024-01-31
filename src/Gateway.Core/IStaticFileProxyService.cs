using Gateway.Core.Entities;
using Gateway.Dto;

namespace Gateway.Core;

public interface IStaticFileProxyService
{
    Task RefreshConfig();
    
    Task<ResultDto> CreateAsync(StaticFileProxyEntity entity);
    
    Task<ResultDto> UpdateAsync(StaticFileProxyEntity entity);
    
    Task<ResultDto> DeleteAsync(string id);
    
    Task<PageResultDto<StaticFileProxyEntity>> GetListAsync(string keyword);
}