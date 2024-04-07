namespace FastGateway;

public sealed class ExceptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (Exception e)
        {
            context.HttpContext.Response.StatusCode = 500;
            return ResultDto.ErrorResult(e);
        }
    }
}