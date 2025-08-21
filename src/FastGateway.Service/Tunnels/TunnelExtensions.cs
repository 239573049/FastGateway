using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace FastGateway.Service.Tunnels;

public static class TunnelExtensions
{
    private static readonly ConcurrentDictionary<string, int> Connections = new();


    /// <summary>
    /// 注册Http2隧道
    /// </summary>
    /// <param name="routes"></param>
    /// <param name="path">监听地址</param>
    /// <param name="connection">连接事件</param>
    /// <param name="brokenLink">关闭连接事件</param>
    /// <param name="getHostName">通过node搜索name</param>
    /// <returns></returns>
    public static IEndpointConventionBuilder MapHttp2Tunnel(this IEndpointRouteBuilder routes, string path,
        Func<string, ValueTask> connection, Func<string, ValueTask> brokenLink, Func<string, string> getHostName)
    {
        return routes.MapPost(path, async (HttpContext context, string host, TunnelClientFactory tunnelFactory,
            IHostApplicationLifetime lifetime) =>
        {
            // HTTP/2 duplex stream
            if (context.Request.Protocol != HttpProtocol.Http2)
            {
                Console.WriteLine("HTTP/2 required");
                return Results.BadRequest();
            }

            // 获取环境变量的password
            var password = Environment.GetEnvironmentVariable("TUNNEL_PASSWORD");

            if (!string.IsNullOrEmpty(password) && context.Request.Query.TryGetValue("password", out var value))
            {
                if (value != password)
                {
                    Console.WriteLine("Password error");
                    return Results.BadRequest();
                }
            }

            await connection.Invoke(host);

            var name = getHostName.Invoke(host);

            if (string.IsNullOrEmpty(name))
            {
                name = getHostName.Invoke(host);
            }

            Connections.AddOrUpdate(name, 1, (key, value) => value + 1);

            var streams = tunnelFactory.GetConnectionChannel(name);
            try
            {
                var stream = new DuplexHttpStream(context);

                await using var reg = lifetime.ApplicationStopping.Register(() => stream.Dispose());

                // Keep reusing this connection while, it's still open on the backend
                while (!context.RequestAborted.IsCancellationRequested)
                {
                    // Make this connection available for requests
                    streams.Add(stream);

                    await stream.StreamCompleteTask;

                    stream.Reset();
                }

                streams.Remove(stream);
            }
            catch (Exception e)
            {
                Console.WriteLine("Failed to create client connection.{0}", e);
            }

            Connections.AddOrUpdate(name, 1, (key, value) => value - 1);

            if (Connections.TryGetValue(name, out var count) && count == 0)
            {
                await brokenLink.Invoke(name);
            }

            return EmptyResult.Instance;
        });
    }

    public static IEndpointConventionBuilder MapWebSocketTunnel(this IEndpointRouteBuilder routes, string path,
        Func<string, ValueTask> connection, Func<string, ValueTask> brokenLink, Func<string, string> getHostName)
    {
        var conventionBuilder = routes.MapGet(path, async (HttpContext context, string host,
            TunnelClientFactory tunnelFactory,
            IHostApplicationLifetime lifetime) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                return Results.BadRequest();
            }

            // 获取环境变量的password
            var password = Environment.GetEnvironmentVariable("TUNNEL_PASSWORD");

            if (!string.IsNullOrEmpty(password) && context.Request.Query.TryGetValue("password", out var value))
            {
                if (value != password)
                {
                    Console.WriteLine("Password error");
                    return Results.BadRequest();
                }
            }

            await connection.Invoke(host);

            var name = getHostName.Invoke(host);

            if (string.IsNullOrEmpty(name))
            {
                name = getHostName.Invoke(host);
            }

            Connections.AddOrUpdate(name, 1, (key, value) => value + 1);

            var responses = tunnelFactory.GetConnectionChannel(name);

            var id = Guid.NewGuid().ToString("N");
            try
            {
                // await requests.Reader.ReadAsync(context.RequestAborted);

                var ws = await context.WebSockets.AcceptWebSocketAsync();

                var stream = new WebSocketStream(ws);

                // We should make this more graceful
                await using var reg = lifetime.ApplicationStopping.Register(() => stream.Abort());

                // Keep reusing this connection while, it's still open on the backend
                while (ws.State == WebSocketState.Open)
                {
                    // Make this connection available for requests
                    // await responses.Writer.WriteAsync(stream, context.RequestAborted);
                    responses.Add(stream);

                    await stream.StreamCompleteTask;

                    stream.Reset();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Connections.AddOrUpdate(name, 1, (key, value) => value - 1);

            if (Connections.TryGetValue(name, out var count) && count == 0)
            {
                await brokenLink.Invoke(name);
            }

            return EmptyResult.Instance;
        });

        // Make this endpoint do websockets automagically as middleware for this specific route
        conventionBuilder.Add(e =>
        {
            var sub = routes.CreateApplicationBuilder();
            sub.UseWebSockets().Run(e.RequestDelegate!);
            e.RequestDelegate = sub.Build();
        });

        return conventionBuilder;
    }

    // This is for .NET 6, .NET 7 has Results.Empty
    internal sealed class EmptyResult : IResult
    {
        internal static readonly EmptyResult Instance = new();

        public Task ExecuteAsync(HttpContext httpContext)
        {
            return Task.CompletedTask;
        }
    }
}