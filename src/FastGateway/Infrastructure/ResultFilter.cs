using System.ComponentModel.DataAnnotations;
using FastGateway.Dto;
using Microsoft.AspNetCore.Mvc;

namespace FastGateway.Infrastructure;

/// <summary>
///     业务结果过滤器
/// </summary>
public sealed class ResultFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        object? result;

        try
        {
            result = await next(context);
        }
        catch (ValidationException ex)
        {
            return ResultDto.CreateFailed(ex.Message);
        }

        if (result is EmptyResult) return null;

        if (result is ResultDto dto) return dto;

        if (result is ResultDto<object> dtoObject) return dtoObject;

        return result == null ? ResultDto.CreateSuccess() : ResultDto.CreateSuccess(result);
    }
}