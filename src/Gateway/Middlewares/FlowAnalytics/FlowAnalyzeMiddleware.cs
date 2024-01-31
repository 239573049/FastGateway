using Microsoft.AspNetCore.Connections;

namespace Gateway.Middlewares.FlowAnalytics;

public sealed class FlowAnalyzeMiddleware(IFlowAnalyzer flowAnalyzer) : IKestrelMiddleware
{
    public async Task InvokeAsync(ConnectionDelegate next, ConnectionContext context)
    {
        var oldTransport = context.Transport;
        try
        {
            await using var duplexPipe = new FlowAnalyzeDuplexPipe(context.Transport, flowAnalyzer);
            context.Transport = duplexPipe;
            await next(context);
        }
        finally
        {
            context.Transport = oldTransport;
        }
    }
}