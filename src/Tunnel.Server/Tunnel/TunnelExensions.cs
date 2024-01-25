﻿using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yarp.ReverseProxy.Forwarder;

public static class TunnelExensions
{
    public static IServiceCollection AddTunnelServices(this IServiceCollection services)
    {
        var tunnelFactory = new TunnelClientFactory();
        services.AddSingleton(tunnelFactory);
        services.AddSingleton<IForwarderHttpClientFactory>(tunnelFactory);
        return services;
    }

    public static IEndpointConventionBuilder MapHttp2Tunnel(this IEndpointRouteBuilder routes, string path)
    {
        return routes.MapPost(path, static async (HttpContext context, string host, TunnelClientFactory tunnelFactory, IHostApplicationLifetime lifetime) =>
        {
            // HTTP/2 duplex stream
            if (context.Request.Protocol != HttpProtocol.Http2)
            {
                return Results.BadRequest();
            }

            var (requests, responses) = tunnelFactory.GetConnectionChannel(host);

            await requests.Reader.ReadAsync(context.RequestAborted);

            var stream = new DuplexHttpStream(context);

            using var reg = lifetime.ApplicationStopping.Register(() => stream.Abort());

            // Keep reusing this connection while, it's still open on the backend
            while (!context.RequestAborted.IsCancellationRequested)
            {
                // Make this connection available for requests
                await responses.Writer.WriteAsync(stream, context.RequestAborted);

                await stream.StreamCompleteTask;

                stream.Reset();
            }

            return EmptyResult.Instance;
        });
    }

    public static IEndpointConventionBuilder MapWebSocketTunnel(this IEndpointRouteBuilder routes, string path)
    {
        var conventionBuilder = routes.MapGet(path, static async (HttpContext context, string host, TunnelClientFactory tunnelFactory, IHostApplicationLifetime lifetime) =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                return Results.BadRequest();
            }

            var (requests, responses) = tunnelFactory.GetConnectionChannel(host);

            await requests.Reader.ReadAsync(context.RequestAborted);

            var ws = await context.WebSockets.AcceptWebSocketAsync();

            var stream = new WebSocketStream(ws);

            // We should make this more graceful
            using var reg = lifetime.ApplicationStopping.Register(() => stream.Abort());

            // Keep reusing this connection while, it's still open on the backend
            while (ws.State == WebSocketState.Open)
            {
                // Make this connection available for requests
                await responses.Writer.WriteAsync(stream, context.RequestAborted);

                await stream.StreamCompleteTask;

                stream.Reset();
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