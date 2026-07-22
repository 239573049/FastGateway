using System.ComponentModel.DataAnnotations;
using FastGateway.Dto;
using Microsoft.AspNetCore.Http.HttpResults;
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

        // 返回 void/Task 的端点由 Minimal API 产出 EmptyHttpResult；MVC 场景为 EmptyResult。
        // 二者都表示“无数据成功”，不能塞进 ResultDto.Data(object)，否则 AOT 源生成器无对应元数据会抛异常。
        if (result is EmptyHttpResult or EmptyResult) return ResultDto.CreateSuccess();

        if (result is ResultDto dto) return dto;

        if (result is ResultDto<object> dtoObject) return dtoObject;

        return result == null ? ResultDto.CreateSuccess() : ResultDto.CreateSuccess(result);
    }
}