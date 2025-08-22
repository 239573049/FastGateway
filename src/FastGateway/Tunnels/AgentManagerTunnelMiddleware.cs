using Microsoft.AspNetCore.Http.Features;

namespace FastGateway.Tunnels;

/// <summary>
///     基于H2的隧道中间件
///     通过隧道ID获取隧道，然后创建隧道，然后花钱H2Stream实现隧道功能。
/// </summary>
/// <param name="agentTunnelFactory"></param>
/// <param name="logger"></param>
public class AgentManagerTunnelMiddleware(
    AgentTunnelFactory agentTunnelFactory,
    ILogger<AgentManagerTunnelMiddleware> logger) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var requiredFeature = context.Features.GetRequiredFeature<IFastFeature>();
        if (!requiredFeature.IsRequest)
        {
            await next(context);
            return;
        }

        var target = context.Features.GetRequiredFeature<IHttpRequestFeature>().RawTarget;
        if (!Guid.TryParse(context.Request.Query["tunnelId"].ToString(), out var tunnelId))
        {
            await next(context);
            return;
        }

        if (agentTunnelFactory.Contains(tunnelId))
        {
            var stream = await requiredFeature.AcceptAsStreamAsync();

            var httpTunnel = new HttpTunnel(stream, tunnelId, requiredFeature.Protocol, logger);

            if (agentTunnelFactory.SetResult(httpTunnel))
                await httpTunnel.Closed;
            else
                httpTunnel.Dispose();
        }
    }
}