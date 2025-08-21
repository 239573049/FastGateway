using System.Diagnostics;
using FastGateway.Entities;

namespace FastGateway.TunnelClient.Monitor;

public class ServerClient(
    MonitorServer monitorServer,
    Tunnel tunnel,
    ILogger<ServerClient> logger)
    : IDisposable
{
    private int _tunnelCount;

    public async Task TransportCoreAsync(Tunnel tunnel, CancellationToken cancellationToken)
    {
        await using var connection =
            await monitorServer.CreateServerConnectionAsync(tunnel, cancellationToken);

        try
        {
            using var linkedTokenSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cancellationToken);
            await foreach (var tunnelId in connection.ReadTunnelIdAsync(cancellationToken))
            {
                BindTunnelIoAsync(tunnelId, linkedTokenSource.Token);
            }
        }
        catch (Exception ex)
        {
            logger.LogError("连接错误！" + ex.Message);
        }
    }

    /// <summary>
    /// 绑定tunnel的IO
    /// </summary> 
    /// <param name="tunnelId"></param>
    /// <param name="cancellationToken"></param>
    private async void BindTunnelIoAsync(Guid tunnelId, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using var targetTunnel = await monitorServer.CreateTargetTunnelAsync(cancellationToken);

            await using var serverTunnel =
                await monitorServer.CreateServerTunnelAsync(tunnel, tunnelId, cancellationToken);

            var count = Interlocked.Increment(ref this._tunnelCount);
            logger.LogWarning("新的隧道id，耗时：" + stopwatch.ElapsedMilliseconds + "ms" + " 当前数量：" + count);

            var server2Target = serverTunnel.CopyToAsync(targetTunnel, cancellationToken);
            var target2Server = targetTunnel.CopyToAsync(serverTunnel, cancellationToken);
            var task = await Task.WhenAny(server2Target, target2Server);
        }
        catch (OperationCanceledException operationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("错误：" + operationCanceledException.Message);
        }
        catch (Exception ex)
        {
            // this.OnTunnelException(ex);
            // Log.LogTunnelError(this.logger, tunnelId, ex.Message);
            logger.LogError("连接异常：" + ex.Message);
        }
        finally
        {
            stopwatch.Stop();
            Interlocked.Decrement(ref _tunnelCount);
        }
    }

    public void Dispose()
    {
    }
}