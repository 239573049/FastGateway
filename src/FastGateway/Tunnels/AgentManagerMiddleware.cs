namespace FastGateway.Tunnels;

/// <summary>
///     节点服务中间件
///     用于与节点服务建立主连接、创建隧道和管理隧道连接、保持ping服务。
/// </summary>
/// <param name="agentTunnelFactory"></param>
/// <param name="logger"></param>
/// <param name="agentClientManager"></param>
internal class AgentManagerMiddleware(
    AgentTunnelFactory agentTunnelFactory,
    ILogger<AgentManagerMiddleware> logger,
    TunnelClientProxy tunnelClientProxy,
    IConfiguration configuration,
    AgentClientManager agentClientManager) : IMiddleware
{
    private readonly string TunnelToken = configuration["TunnelToken"] ?? "Aa123456.";

    /// <summary>
    ///     HTTP请求中间件的入口方法
    /// </summary>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // 创建自定义特性并判断协议是否允许
        var feature = new FastFeature(context);
        if (IsAllowProtocol(feature.Protocol))
        {
            // 设置特性
            context.Features.Set<IFastFeature>(feature);
        }
        else
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            return;
        }

        // 从请求中获取节点ID
        var nodeName = context.Request.Query["nodeName"].ToString();

        // 如果节点ID为空，调用下一个中间件
        if (string.IsNullOrEmpty(nodeName))
        {
            await next(context);
            return;
        }

        var token = context.Request.Query["token"].ToString();
        if (string.IsNullOrEmpty(token))
        {
            await Task.Delay(5000);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }


        if (TunnelToken != token)
        {
            await Task.Delay(5000);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        // 构建应用程序主机名
        var host = "node_" + nodeName;

        try
        {
            // 创建连接
            var stream = await feature.AcceptAsSafeWriteStreamAsync();

            // 创建客户端连接
            var connection = new AgentClientConnection(host, stream, new ConnectionConfig(), logger);

            var disconnected = false;

            // 使用连接创建客户端对象，并添加到客户端管理器
            await using var client = new AgentClient(connection, agentTunnelFactory, context);
            if (await agentClientManager.AddAsync(client, default))
            {
                // 等待连接关闭
                await connection.WaitForCloseAsync();

                // 从客户管理器中移除客户端对象
                disconnected = await agentClientManager.RemoveAsync(client, default);
            }
        }
        catch (Exception e)
        {
            // 记录错误日志
            logger.LogError(e, "Failed to create client connection.");
        }
        finally
        {
            // NodesServiceRegistrationService.ClientList.TryRemove(nodeId, out _);
            // await NodesServiceRegistrationService.UnregisterClient(host);
            await tunnelClientProxy.RemoveClientAsync(nodeName);
        }
    }

    /// <summary>
    ///     校验协议是否为允许的类型
    /// </summary>
    private static bool IsAllowProtocol(TransportProtocol protocol)
    {
        return protocol is TransportProtocol.Http11 or TransportProtocol.Http2 or TransportProtocol.WebSocketWithHttp11
            or TransportProtocol.WebSocketWithHttp2;
    }
}