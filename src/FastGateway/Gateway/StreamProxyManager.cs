using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using Core.Entities;
using Core.Entities.Core;
using FastGateway.Dto;
using FastGateway.Services;

namespace FastGateway.Gateway;

/// <summary>
///     L4 端口转发运行时（TCP/UDP 裸转发）。
///     镜像 <see cref="Gateway" /> 的静态管理类模式：用字典跟踪运行中的转发器，
///     暴露 Start/Stop/Reload/CheckOnline/GetStats，与 HTTP 网关生命周期对称。
/// </summary>
public static class StreamProxyManager
{
    private static readonly ConcurrentDictionary<string, StreamForwardRunner> Runners = new();

    /// <summary>
    ///     启动（或忽略未启用/已在线的）转发规则。
    /// </summary>
    public static Task StartAsync(StreamForward rule)
    {
        if (rule == null || !rule.Enable) return Task.CompletedTask;
        if (string.IsNullOrEmpty(rule.Id)) return Task.CompletedTask;
        if (Runners.ContainsKey(rule.Id)) return Task.CompletedTask;

        if (rule.UpStreams == null || rule.UpStreams.Count == 0)
        {
            Console.WriteLine($"端口转发[{rule.Name}]未配置上游目标，跳过启动。");
            return Task.CompletedTask;
        }

        var runner = new StreamForwardRunner(rule);
        if (!Runners.TryAdd(rule.Id, runner)) return Task.CompletedTask;

        try
        {
            runner.Start();
        }
        catch (Exception e)
        {
            Console.WriteLine($"端口转发[{rule.Name}]启动错误：{e}");
            Runners.TryRemove(rule.Id, out _);
            runner.Dispose();
        }

        return Task.CompletedTask;
    }

    /// <summary>
    ///     停止并释放转发规则的监听。
    /// </summary>
    public static Task StopAsync(string id)
    {
        if (Runners.TryRemove(id, out var runner)) runner.Dispose();
        return Task.CompletedTask;
    }

    /// <summary>
    ///     检查转发规则是否在线。
    /// </summary>
    public static bool CheckOnline(string id)
    {
        return Runners.ContainsKey(id);
    }

    /// <summary>
    ///     重载：L4 无需热更新，直接重建监听。
    /// </summary>
    public static async Task ReloadAsync(StreamForward rule)
    {
        await StopAsync(rule.Id);
        await StartAsync(rule);
    }

    /// <summary>
    ///     获取运行状态（在线、活动连接数、UDP 会话数）。
    /// </summary>
    public static StreamForwardStatsDto GetStats(string id)
    {
        var (online, activeConnections, udpSessions) = GetRuntimeStats(id);
        return new StreamForwardStatsDto
        {
            Online = online,
            ActiveConnections = activeConnections,
            UdpSessions = udpSessions
        };
    }

    /// <summary>
    ///     获取运行状态（供 DTO 映射）。
    /// </summary>
    public static (bool online, int activeConnections, int udpSessions) GetRuntimeStats(string id)
    {
        if (Runners.TryGetValue(id, out var runner))
            return (true, runner.ActiveConnections, runner.UdpSessions);

        return (false, 0, 0);
    }

    /// <summary>
    ///     单条规则的运行器：按协议起 TCP/UDP 监听，管理生命周期与统计。
    /// </summary>
    private sealed class StreamForwardRunner : IDisposable
    {
        private const int TcpBufferSize = 64 * 1024;
        private const int UdpBufferSize = 64 * 1024;

        private readonly StreamForward _rule;
        private readonly CancellationTokenSource _cts = new();
        private readonly int[] _upstreamConnections;
        private readonly ConcurrentDictionary<SocketAddress, UdpSession> _udpSessions = new();

        private int _activeConnections;
        private int _roundRobin = -1;
        private Socket? _tcpListener;
        private Socket? _udpListener;

        public StreamForwardRunner(StreamForward rule)
        {
            _rule = rule;
            _upstreamConnections = new int[rule.UpStreams.Count];
        }

        public int ActiveConnections => Volatile.Read(ref _activeConnections);
        public int UdpSessions => _udpSessions.Count;

        public void Start()
        {
            var listenIp = IPAddress.TryParse(_rule.ListenAddress, out var ip) ? ip : IPAddress.Any;

            if (_rule.Protocol is StreamProtocol.Tcp or StreamProtocol.Both)
            {
                _tcpListener = new Socket(listenIp.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _tcpListener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _tcpListener.Bind(new IPEndPoint(listenIp, _rule.ListenPort));
                _tcpListener.Listen(512);
                _ = AcceptTcpLoopAsync(_tcpListener, _cts.Token);
            }

            if (_rule.Protocol is StreamProtocol.Udp or StreamProtocol.Both)
            {
                _udpListener = new Socket(listenIp.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                _udpListener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpListener.Bind(new IPEndPoint(listenIp, _rule.ListenPort));
                _ = ReceiveUdpLoopAsync(_udpListener, _cts.Token);
                _ = SweepUdpSessionsLoopAsync(_cts.Token);
            }
        }

        #region TCP

        private async Task AcceptTcpLoopAsync(Socket listener, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                Socket client;
                try
                {
                    client = await listener.AcceptAsync(ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception)
                {
                    // 单次 accept 失败不影响循环
                    continue;
                }

                _ = HandleTcpClientAsync(client, ct);
            }
        }

        private async Task HandleTcpClientAsync(Socket client, CancellationToken ct)
        {
            Socket? upstream = null;
            var upstreamIndex = -1;
            try
            {
                // 来源 IP 访问控制
                var clientIp = (client.RemoteEndPoint as IPEndPoint)?.Address?.ToString();
                if (!BlacklistAndWhitelistService.IsAllowed(clientIp, _rule.EnableBlacklist, _rule.EnableWhitelist))
                {
                    SafeClose(client);
                    return;
                }

                client.NoDelay = true;

                (upstream, upstreamIndex) = await ConnectUpstreamAsync(ct);
                if (upstream == null)
                {
                    SafeClose(client);
                    return;
                }

                Interlocked.Increment(ref _activeConnections);

                using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                if (_rule.IdleTimeoutSeconds > 0)
                    connectionCts.CancelAfter(TimeSpan.FromSeconds(_rule.IdleTimeoutSeconds));

                var token = connectionCts.Token;
                var c2u = PumpAsync(client, upstream, connectionCts, token);
                var u2c = PumpAsync(upstream, client, connectionCts, token);
                await Task.WhenAll(c2u, u2c);
            }
            catch
            {
                // 连接级异常忽略
            }
            finally
            {
                if (upstream != null)
                {
                    if (upstreamIndex >= 0) Interlocked.Decrement(ref _upstreamConnections[upstreamIndex]);
                    Interlocked.Decrement(ref _activeConnections);
                    SafeClose(upstream);
                }

                SafeClose(client);
            }
        }

        /// <summary>
        ///     单向数据泵：读到 EOF 时对目标传播半关闭；有数据则刷新空闲计时。
        /// </summary>
        private async Task PumpAsync(Socket from, Socket to, CancellationTokenSource idleCts, CancellationToken token)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(TcpBufferSize);
            var resetIdle = _rule.IdleTimeoutSeconds > 0;
            var idle = TimeSpan.FromSeconds(_rule.IdleTimeoutSeconds);
            try
            {
                while (true)
                {
                    var read = await from.ReceiveAsync(buffer.AsMemory(0, TcpBufferSize), SocketFlags.None, token);
                    if (read == 0) break; // 对端关闭写通道

                    if (resetIdle) idleCts.CancelAfter(idle);

                    var sent = 0;
                    while (sent < read)
                        sent += await to.SendAsync(buffer.AsMemory(sent, read - sent), SocketFlags.None, token);
                }

                // 传播半关闭：告知对端本方向已结束
                try { to.Shutdown(SocketShutdown.Send); } catch { /* ignore */ }
            }
            catch
            {
                // 取消或 socket 错误：由 finally 与调用方统一收尾
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task<(Socket? socket, int index)> ConnectUpstreamAsync(CancellationToken ct)
        {
            var count = _rule.UpStreams.Count;
            var start = SelectUpstreamIndex(count);

            // 从负载均衡选中的目标开始，失败则依次尝试其余上游（故障剔除）
            for (var attempt = 0; attempt < count; attempt++)
            {
                var index = (start + attempt) % count;
                var up = _rule.UpStreams[index];

                try
                {
                    var ep = await ResolveEndPointAsync(up.Host, up.Port, ct);
                    var socket = new Socket(ep.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

                    using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    if (_rule.ConnectTimeoutMs > 0) connectCts.CancelAfter(_rule.ConnectTimeoutMs);

                    try
                    {
                        await socket.ConnectAsync(ep, connectCts.Token);
                    }
                    catch
                    {
                        SafeClose(socket);
                        continue;
                    }

                    Interlocked.Increment(ref _upstreamConnections[index]);
                    return (socket, index);
                }
                catch
                {
                    // DNS 解析等失败，尝试下一个上游
                }
            }

            return (null, -1);
        }

        #endregion

        #region UDP

        private async Task ReceiveUdpLoopAsync(Socket listener, CancellationToken ct)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(UdpBufferSize);
            var receivedAddress = new SocketAddress(listener.AddressFamily);
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int received;
                    try
                    {
                        received = await listener.ReceiveFromAsync(buffer.AsMemory(0, UdpBufferSize),
                            SocketFlags.None, receivedAddress, ct);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }
                    catch (SocketException)
                    {
                        // 单个数据报错误（如 ICMP 端口不可达）不应终止循环
                        continue;
                    }

                    var session = await GetOrCreateUdpSessionAsync(listener, receivedAddress, ct);
                    if (session == null) continue;

                    try
                    {
                        await session.UpstreamSocket.SendAsync(buffer.AsMemory(0, received), SocketFlags.None, ct);
                        session.Touch();
                    }
                    catch
                    {
                        // 上游发送失败：移除会话，下个包会重建
                        RemoveUdpSession(session);
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private async Task<UdpSession?> GetOrCreateUdpSessionAsync(Socket listener, SocketAddress source,
            CancellationToken ct)
        {
            if (_udpSessions.TryGetValue(source, out var existing)) return existing;

            // 新客户端首包：做来源 IP 访问控制
            IPEndPoint clientEndPoint;
            try
            {
                clientEndPoint = (IPEndPoint)new IPEndPoint(IPAddress.Any, 0).Create(source);
            }
            catch
            {
                return null;
            }

            if (!BlacklistAndWhitelistService.IsAllowed(clientEndPoint.Address.ToString(),
                    _rule.EnableBlacklist, _rule.EnableWhitelist))
                return null;

            var count = _rule.UpStreams.Count;
            var start = SelectUpstreamIndex(count);
            var up = _rule.UpStreams[start];

            Socket upstream;
            try
            {
                var ep = await ResolveEndPointAsync(up.Host, up.Port, ct);
                upstream = new Socket(ep.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
                await upstream.ConnectAsync(ep, ct); // 设置默认远端，后续用 Send/Receive
            }
            catch
            {
                return null;
            }

            var key = CloneAddress(source);
            var session = new UdpSession(key, upstream);

            if (!_udpSessions.TryAdd(key, session))
            {
                // 并发竞争：已有会话，丢弃本次创建
                session.Dispose();
                _udpSessions.TryGetValue(source, out session);
                return session;
            }

            _ = RelayUdpBackAsync(listener, session, _cts.Token);
            return session;
        }

        private async Task RelayUdpBackAsync(Socket listener, UdpSession session, CancellationToken ct)
        {
            var buffer = ArrayPool<byte>.Shared.Rent(UdpBufferSize);
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int n;
                    try
                    {
                        n = await session.UpstreamSocket.ReceiveAsync(buffer.AsMemory(0, UdpBufferSize),
                            SocketFlags.None, ct);
                    }
                    catch
                    {
                        break;
                    }

                    session.Touch();
                    try
                    {
                        await listener.SendToAsync(buffer.AsMemory(0, n), SocketFlags.None, session.ClientAddress, ct);
                    }
                    catch
                    {
                        break;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
                RemoveUdpSession(session);
            }
        }

        private async Task SweepUdpSessionsLoopAsync(CancellationToken ct)
        {
            var idleMs = Math.Max(1, _rule.IdleTimeoutSeconds) * 1000L;
            var interval = TimeSpan.FromSeconds(Math.Clamp(_rule.IdleTimeoutSeconds / 2, 5, 60));
            using var timer = new PeriodicTimer(interval);
            try
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    var now = Environment.TickCount64;
                    foreach (var kvp in _udpSessions)
                        if (now - kvp.Value.LastActivityTicks > idleMs)
                            RemoveUdpSession(kvp.Value);
                }
            }
            catch (OperationCanceledException)
            {
                // 停止
            }
        }

        private void RemoveUdpSession(UdpSession session)
        {
            if (_udpSessions.TryRemove(session.ClientAddress, out _)) session.Dispose();
        }

        #endregion

        #region Helpers

        private int SelectUpstreamIndex(int count)
        {
            if (count <= 1) return 0;

            switch (_rule.LoadBalancing)
            {
                case StreamLoadBalancing.Random:
                    return Random.Shared.Next(count);
                case StreamLoadBalancing.LeastConnections:
                    var minIndex = 0;
                    var min = Volatile.Read(ref _upstreamConnections[0]);
                    for (var i = 1; i < count; i++)
                    {
                        var c = Volatile.Read(ref _upstreamConnections[i]);
                        if (c < min)
                        {
                            min = c;
                            minIndex = i;
                        }
                    }

                    return minIndex;
                case StreamLoadBalancing.RoundRobin:
                default:
                    var next = Interlocked.Increment(ref _roundRobin);
                    return (int)((uint)next % (uint)count);
            }
        }

        private static async ValueTask<IPEndPoint> ResolveEndPointAsync(string host, int port, CancellationToken ct)
        {
            if (IPAddress.TryParse(host, out var ip)) return new IPEndPoint(ip, port);

            var addresses = await Dns.GetHostAddressesAsync(host, ct);
            if (addresses.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
            return new IPEndPoint(addresses[0], port);
        }

        private static SocketAddress CloneAddress(SocketAddress src)
        {
            var dst = new SocketAddress(src.Family, src.Size);
            for (var i = 0; i < src.Size; i++) dst[i] = src[i];
            return dst;
        }

        private static void SafeClose(Socket socket)
        {
            try { socket.Dispose(); }
            catch { /* ignore */ }
        }

        #endregion

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { /* ignore */ }

            if (_tcpListener != null) SafeClose(_tcpListener);
            if (_udpListener != null) SafeClose(_udpListener);

            foreach (var kvp in _udpSessions)
                if (_udpSessions.TryRemove(kvp.Key, out var session))
                    session.Dispose();

            try { _cts.Dispose(); } catch { /* ignore */ }
        }
    }

    /// <summary>
    ///     UDP 会话：客户端地址 ↔ 一个连向上游的专属 socket。
    /// </summary>
    private sealed class UdpSession : IDisposable
    {
        public UdpSession(SocketAddress clientAddress, Socket upstreamSocket)
        {
            ClientAddress = clientAddress;
            UpstreamSocket = upstreamSocket;
            LastActivityTicks = Environment.TickCount64;
        }

        public SocketAddress ClientAddress { get; }
        public Socket UpstreamSocket { get; }
        public long LastActivityTicks { get; private set; }

        public void Touch()
        {
            LastActivityTicks = Environment.TickCount64;
        }

        public void Dispose()
        {
            try { UpstreamSocket.Dispose(); }
            catch { /* ignore */ }
        }
    }
}
