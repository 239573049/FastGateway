using System.Diagnostics.CodeAnalysis;
using FastGateway.Entities;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;

namespace FastGateway.Service.Tunnels;

public sealed class FastFeature : IFastFeature
{
    private readonly Func<Task<Stream>>? _acceptAsyncFunc;

    public bool IsRequest => this._acceptAsyncFunc != null;

    public TransportProtocol Protocol { get; }

    public FastFeature(HttpContext context)
    {
        if (TryGetHttp2Feature(context, out var protocol, out var acceptAsync) ||
            TryGetHttp11Feature(context, out protocol, out acceptAsync) ||
            TryGetWebSocketFeature(context, out protocol, out acceptAsync))
        {
            this.Protocol = protocol;
            this._acceptAsyncFunc = acceptAsync;
        }
    }

    /// <summary>
    /// :method = CONNECT
    /// :protocol = CYarp
    /// :scheme = https
    /// </summary>
    private static bool TryGetHttp2Feature(
        HttpContext context,
        [MaybeNullWhen(false)] out TransportProtocol protocol,
        [MaybeNullWhen(false)] out Func<Task<Stream>>? acceptAsync)
    {
        var http2Feature = context.Features.Get<IHttpExtendedConnectFeature>();
        if (http2Feature is { IsExtendedConnect: true } &&
            string.Equals(Constant.Protocol, http2Feature.Protocol, StringComparison.InvariantCultureIgnoreCase))
        {
            protocol = TransportProtocol.Http2;
            acceptAsync = AcceptAsync;
            return true;
        }
        else
        {
            protocol = default;
            acceptAsync = default;
            return default;
        }

        async Task<Stream> AcceptAsync()
        {
            context.Features.Get<IHttpRequestTimeoutFeature>()?.DisableTimeout();
            return await http2Feature.AcceptAsync();
        }
    }

    private static bool TryGetWebSocketFeature(
        HttpContext context,
        [MaybeNullWhen(false)] out TransportProtocol protocol,
        [MaybeNullWhen(false)] out Func<Task<Stream>>? acceptAsync)
    {
        var webSocketManager = context.WebSockets;
        if (webSocketManager.IsWebSocketRequest &&
            webSocketManager.WebSocketRequestedProtocols.Contains(Constant.Protocol,
                StringComparer.InvariantCultureIgnoreCase))
        {
            protocol = context.Request.Protocol == HttpProtocol.Http2
                ? TransportProtocol.WebSocketWithHttp2
                : TransportProtocol.WebSocketWithHttp11;

            acceptAsync = AcceptAsync;
            return true;
        }
        else
        {
            protocol = default;
            acceptAsync = default;
            return default;
        }

        async Task<Stream> AcceptAsync()
        {
            var webSocket = await webSocketManager.AcceptWebSocketAsync(Constant.Protocol);
            return new WebSocketStream(webSocket);
        }
    }

    /// <summary>
    /// Get {PATH} HTTP/1.1
    /// Connection: Upgrade
    /// Upgrade: CYarp  
    /// </summary>
    private static bool TryGetHttp11Feature(
        HttpContext context,
        [MaybeNullWhen(false)] out TransportProtocol protocol,
        [MaybeNullWhen(false)] out Func<Task<Stream>>? acceptAsync)
    {
        var http11Feature = context.Features.GetRequiredFeature<IHttpUpgradeFeature>();
        if (http11Feature.IsUpgradableRequest &&
            string.Equals(Constant.Protocol, context.Request.Headers.Upgrade,
                StringComparison.InvariantCultureIgnoreCase))
        {
            protocol = TransportProtocol.Http11;
            acceptAsync = AcceptAsync;
            return true;
        }
        else
        {
            protocol = default;
            acceptAsync = default;
            return default;
        }

        Task<Stream> AcceptAsync()
        {
            context.Features.Get<IHttpRequestTimeoutFeature>()?.DisableTimeout();
            return http11Feature.UpgradeAsync();
        }
    }

    public Task<Stream> AcceptAsStreamAsync()
    {
        return this._acceptAsyncFunc == null
            ? throw new InvalidOperationException("Not a valid request for accepting a stream.")
            : this._acceptAsyncFunc();
    }

    public async Task<Stream> AcceptAsSafeWriteStreamAsync()
    {
        var stream = await this.AcceptAsStreamAsync();
        return new SafeWriteStream(stream);
    }
}