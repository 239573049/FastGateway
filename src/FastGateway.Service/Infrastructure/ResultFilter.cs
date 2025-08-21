using FastGateway.Service.Dto;
using Microsoft.AspNetCore.Mvc;

namespace FastGateway.Service.Infrastructure;

/// <summary>
/// 业务结果过滤器
/// </summary>
public sealed class ResultFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var result = await next(context);

        if (result is EmptyResult)
        {
            return null;
        }

        if (result is ResultDto dto)
        {
            return dto;
        }
            
        if(result is ResultDto<object> dtoObject)
        {
            return dtoObject;
        }

        return result == null ? ResultDto.CreateSuccess() : ResultDto.CreateSuccess(result);
    }
}