using Microsoft.AspNetCore.Connections;

namespace FastGateway.Middlewares;

/// <summary>
///     Kestrel的中间件接口
/// </summary>
public interface IKestrelMiddleware
{
    Task InvokeAsync(ConnectionDelegate next, ConnectionContext context);
}