using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Core;
using TunnelClient.Model;

namespace TunnelClient.Monitor;

public class MonitorServer(IServiceProvider serviceProvider)
{
    private readonly HttpMessageInvoker _httpClient = new(CreateDefaultHttpHandler(), true);
    private readonly ILogger<MonitorServer> _logger = serviceProvider.GetRequiredService<ILogger<MonitorServer>>();

    private static SocketsHttpHandler CreateDefaultHttpHandler()
    {
        return new SocketsHttpHandler
        {
            // 允许多个http2连接
            EnableMultipleHttp2Connections = true,
            // 设置连接超时时间
            ConnectTimeout = TimeSpan.FromSeconds(60),
            SslOptions = new SslClientAuthenticationOptions
            {
                // 由于我们没有证书，所以我们需要设置为true
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        };
    }

    public async Task<Stream> CreateTargetTunnelAsync(Tunnel tunnel, CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            EndPoint endPoint = new DnsEndPoint("localhost", tunnel.Port);

            using var linkedTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None, cancellationToken);

            await socket.ConnectAsync(endPoint, linkedTokenSource.Token);
            return new NetworkStream(socket);
        }
        catch (Exception ex)
        {
            Log.CreateTargetTunnelFailed(_logger, ex.Message);
            socket.Dispose();
            throw;
        }
    }

    private async Task<Stream> HttpConnectServerCoreAsync(Tunnel? tunnel,
        Guid? tunnelId,
        CancellationToken cancellationToken)
    {
        if (tunnel?.IsHttp2 == true) return await Http20ConnectServerAsync(tunnel, tunnelId, cancellationToken);

        if (tunnel?.IsWebSocket == true)
            return await HttpWebSocketConnectServerAsync(tunnel, tunnelId, cancellationToken);

        Log.UnsupportedConnectionType(_logger, tunnel?.Type);
        throw new NotSupportedException("不支持的连接类型，只支持http2和websocket连接。请检查Tunnel配置。");
    }

    public async Task<ServerConnection> CreateServerConnectionAsync(Tunnel tunnel,
        CancellationToken cancellationToken)
    {
        var stream = await HttpConnectServerCoreAsync(tunnel, null, cancellationToken);
        var safeWriteStream = new SafeWriteStream(stream);
        return new ServerConnection(safeWriteStream, TimeSpan.FromSeconds(60),
            serviceProvider.GetRequiredService<ILogger<ServerConnection>>());
    }

    public async Task<Stream> ConnectServerAsync(Tunnel tunnel, Guid? tunnelId, CancellationToken cancellationToken)
    {
        return await HttpConnectServerCoreAsync(tunnel, tunnelId, cancellationToken);
    }

    /// <summary>
    ///     创建到服务器的通道
    /// </summary>
    /// <param name="tunnel"></param>
    /// <param name="tunnelId"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <returns></returns>
    public async Task<Stream> CreateServerTunnelAsync(Tunnel tunnel, Guid tunnelId,
        CancellationToken cancellationToken)
    {
        var stream = await ConnectServerAsync(tunnel, tunnelId, cancellationToken);
        return new ForceFlushStream(stream);
    }

    /// <summary>
    ///     创建http2连接
    /// </summary>
    /// <param name="tunnel"></param>
    /// <param name="tunnelId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<Stream> Http20ConnectServerAsync(Tunnel? tunnel,
        Guid? tunnelId,
        CancellationToken cancellationToken)
    {
        Uri serverUri;
        if (tunnelId == null)
            serverUri = new Uri($"{tunnel.ServerUrl.TrimEnd('/')}/internal/gateway/Server?nodeName=" + tunnel.Name +
                                "&token=" +
                                tunnel?.Token);
        else
            serverUri = new Uri($"{tunnel.ServerUrl.TrimEnd('/')}/internal/gateway/Server?tunnelId=" + tunnelId);

        // 这里我们使用Connect方法，因为我们需要建立一个双工流, 这样我们就可以进行双工通信了。
        var request = new HttpRequestMessage(HttpMethod.Connect, serverUri);
        // 如果设置了Connect，那么我们需要设置Protocol
        request.Headers.Protocol = Constant.Protocol;
        // 我们需要设置http2的版本
        request.Version = HttpVersion.Version20;

        // 我们需要确保我们的请求是http2的
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

        // 设置一下超时时间，这样我们就可以在超时的时候取消连接了。
        using var timeoutTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var linkedTokenSource =
            CancellationTokenSource.CreateLinkedTokenSource(timeoutTokenSource.Token, cancellationToken);

        // 发送请求，然后等待响应
        var httpResponse = await _httpClient.SendAsync(request, linkedTokenSource.Token);

        if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            Log.UnauthorizedAccess(_logger);
            throw new UnauthorizedAccessException("未授权,请检查token是否正确");
        }

        // 返回h2的流，用于传输数据
        return await httpResponse.Content.ReadAsStreamAsync(linkedTokenSource.Token);
    }

    private async Task<Stream> HttpWebSocketConnectServerAsync(Tunnel? tunnel,
        Guid? tunnelId,
        CancellationToken cancellationToken)
    {
        Uri serverUri;
        if (tunnelId == null)
            serverUri = new Uri($"{tunnel.ServerUrl.TrimEnd('/')}/internal/gateway/Server?nodeName=" + tunnel.Name +
                                "&token=" +
                                tunnel?.Token);
        else
            serverUri = new Uri($"{tunnel.ServerUrl.TrimEnd('/')}/internal/gateway/Server?tunnelId=" + tunnelId);

        var webSocket = new ClientWebSocket();
        webSocket.Options.AddSubProtocol(Constant.Protocol);

        if (tunnel?.ServerHttp2Support != false && serverUri.Scheme == Uri.UriSchemeWss)
        {
            webSocket.Options.HttpVersion = HttpVersion.Version20;
            webSocket.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
        }
        else
        {
            webSocket.Options.HttpVersion = HttpVersion.Version11;
            webSocket.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
        }

        if (tunnelId == null)
        {
            webSocket.Options.SetRequestHeader("nodeName", tunnel?.Name ?? string.Empty);
            webSocket.Options.SetRequestHeader("token", tunnel?.Token ?? string.Empty);
            webSocket.Options.SetRequestHeader("clientType", "TunnelClient");
            webSocket.Options.SetRequestHeader("requestId", Guid.NewGuid().ToString());
            // 增加请求头信息以便于识别websocket
        }

        try
        {
            await webSocket.ConnectAsync(serverUri, _httpClient, cancellationToken);
            return new WebSocketStream(webSocket);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            webSocket.Dispose();
            throw;
        }
        catch (Exception)
        {
            webSocket.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     注册节点
    /// </summary>
    public async Task RegisterNodeAsync(Tunnel tunnel, CancellationToken cancellationToken)
    {
        using var httpClient = new HttpClient();

        var serverUri =
            new Uri($"{tunnel.ServerUrl.TrimEnd('/')}/internal/gateway/Server/register?token=" + tunnel.Token);

        var str = new StringContent(JsonSerializer.Serialize(tunnel, AppContext.Default.Options), Encoding.UTF8,
            "application/json");

        // 这里我们使用PostAsync方法，因为我们需要发送一个POST请求来注册节点
        var response =
            await httpClient.PostAsync(serverUri, str, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            Log.UnauthorizedAccess(_logger);
            throw new UnauthorizedAccessException("未授权,请检查token是否正确");
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            Log.RegisterNodeFailed(_logger, error);
            throw new InvalidOperationException($"注册节点失败: {error}");
        }

        var result = await response.Content.ReadAsStringAsync(cancellationToken);

        Log.RegisterNodeSuccess(_logger);
    }
}

internal static partial class Log
{
    [LoggerMessage(LogLevel.Error, "创建targetTunnel隧道失败。{Message}")]
    public static partial void CreateTargetTunnelFailed(ILogger logger, string message);

    [LoggerMessage(LogLevel.Information, "注册节点成功。")]
    public static partial void RegisterNodeSuccess(ILogger logger);

    [LoggerMessage(LogLevel.Error, "不支持的连接类型，只支持http2和websocket连接。请检查Tunnel配置，当前协议:{type}")]
    public static partial void UnsupportedConnectionType(ILogger logger, string type);

    [LoggerMessage(LogLevel.Error, "注册节点失败: {message}")]
    public static partial void RegisterNodeFailed(ILogger logger, string message);

    [LoggerMessage(LogLevel.Error, "未授权,请检查token是否正确")]
    public static partial void UnauthorizedAccess(ILogger logger);
}