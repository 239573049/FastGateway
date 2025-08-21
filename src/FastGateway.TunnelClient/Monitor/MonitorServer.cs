using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FastGateway.Entities;

namespace FastGateway.TunnelClient.Monitor;

public class MonitorServer
{
    private readonly HttpMessageInvoker _httpClient = new(CreateDefaultHttpHandler(), true);

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
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            },
        };
    }

    public async Task<Stream> CreateTargetTunnelAsync(CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            EndPoint endPoint = new DnsEndPoint("127.0.0.1", 60899);

            using var linkedTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(CancellationToken.None, cancellationToken);

            await socket.ConnectAsync(endPoint, linkedTokenSource.Token);
            return new NetworkStream(socket);
        }
        catch (Exception ex)
        {
            Console.WriteLine("创建targetTunnel隧道失败。");
            Console.WriteLine(ex);
            socket.Dispose();
            throw;
        }
    }

    private async Task<Stream> HttpConnectServerCoreAsync(Tunnel? tunnel,
        Guid? tunnelId,
        CancellationToken cancellationToken)
    {
        if (tunnel?.IsHttp2 == true)
        {
            return await this.Http20ConnectServerAsync(tunnel, tunnelId, cancellationToken);
        }
        else if (tunnel?.IsWebSocket == true)
        {
            return await this.HttpWebSocketConnectServerAsync(tunnel, tunnelId, cancellationToken);
        }

        throw new NotSupportedException("不支持的连接类型，只支持http2和websocket连接。请检查Tunnel配置。");
    }

    public async Task<ServerConnection> CreateServerConnectionAsync(Tunnel tunnel,
        CancellationToken cancellationToken)
    {
        var stream = await this.HttpConnectServerCoreAsync(tunnel, null, cancellationToken);
        var safeWriteStream = new SafeWriteStream(stream);
        return new ServerConnection(safeWriteStream, TimeSpan.FromSeconds(60));
    }

    public async Task<Stream> ConnectServerAsync(Tunnel tunnel, Guid? tunnelId, CancellationToken cancellationToken)
    {
        return await this.HttpConnectServerCoreAsync(tunnel, tunnelId, cancellationToken);
    }

    /// <summary>
    /// 创建到服务器的通道
    /// </summary>
    /// <param name="tunnel"></param>
    /// <param name="tunnelId"></param>
    /// <param name="cancellationToken"></param>
    /// <exception cref="OperationCanceledException"></exception>
    /// <returns></returns>
    public async Task<Stream> CreateServerTunnelAsync(Tunnel tunnel, Guid tunnelId,
        CancellationToken cancellationToken)
    {
        var stream = await this.ConnectServerAsync(tunnel, tunnelId, cancellationToken);
        return new ForceFlushStream(stream);
    }

    /// <summary>
    /// 创建http2连接
    /// </summary>
    /// <param name="server"></param>
    /// <param name="clientId"></param>
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
        {
            serverUri = new Uri($"{tunnel.ServerUrl.TrimEnd('/')}/internal/gateway/Server?nodeName=" + tunnel.Name +
                                "&token=" +
                                tunnel?.Token);
        }
        else
        {
            serverUri = new Uri($"{tunnel.ServerUrl.TrimEnd('/')}/internal/gateway/Server?tunnelId=" + tunnelId);
        }

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
        var httpResponse = await this._httpClient.SendAsync(request, linkedTokenSource.Token);

        if (httpResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
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
        {
            serverUri = new Uri($"{tunnel.ServerUrl.TrimEnd('/')}/internal/gateway/Server?nodeName=" + tunnel.Name +
                                "&token=" +
                                tunnel?.Token);
        }
        else
        {
            serverUri = new Uri($"{tunnel.ServerUrl.TrimEnd('/')}/internal/gateway/Server?tunnelId=" + tunnelId);
        }

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
    /// 注册节点
    /// </summary>
    public static async Task RegisterNodeAsync(Tunnel tunnel, CancellationToken cancellationToken)
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
            throw new UnauthorizedAccessException("未授权,请检查token是否正确");
        }

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"注册节点失败: {error}");
        }

        var result = await response.Content.ReadAsStringAsync(cancellationToken);

        Console.WriteLine("注册节点成功");
    }
}