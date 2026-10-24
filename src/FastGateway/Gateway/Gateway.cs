using Core.Entities;
using Core.Entities.Core;
using FastGateway.Cluster;
using FastGateway.Dto;
using FastGateway.Extensions;
using FastGateway.Infrastructure;
using FastGateway.Middleware;
using FastGateway.Options;
using FastGateway.Risk;
using FastGateway.Services;
using FastGateway.Tunnels;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.WebSockets;
using Microsoft.Extensions.Primitives;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.LoadBalancing;
using Yarp.ReverseProxy.Model;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

namespace FastGateway.Gateway;

/// <summary>
///     网关服务
/// </summary>
public static class Gateway
{
    private const string Root = "Root";
    private const string GatewayVersionHeader = "X-FastGateway-Version";
    private const string ClientModeMetadataKey = "FastGateway.ClientMode";
    private const string TunnelClientMode = "Tunnel";
    private const string StandardClientMode = "Standard";
    private static readonly ConcurrentDictionary<string, WebApplication> GatewayWebApplications = new();

    private static readonly DestinationConfig StaticProxyDestination = new() { Address = "http://127.0.0.1" };

    private static HealthCheckConfig? CreateHealthCheckConfig(DomainName domainName)
    {
        if (!domainName.EnableHealthCheck) return null;

        var healthCheckPath = domainName.HealthCheckPath?.Trim();
        if (string.IsNullOrWhiteSpace(healthCheckPath)) return null;

        if (!healthCheckPath.StartsWith('/'))
            healthCheckPath = "/" + healthCheckPath;

        string? path = healthCheckPath;
        string? query = null;

        var queryIndex = healthCheckPath.IndexOf('?');
        if (queryIndex >= 0)
        {
            path = healthCheckPath[..queryIndex];
            query = healthCheckPath[queryIndex..];
            if (string.IsNullOrWhiteSpace(query) || query == "?") query = null;
        }

        if (string.IsNullOrWhiteSpace(path)) path = "/";

        var intervalSeconds = domainName.HealthCheckIntervalSeconds;
        if (intervalSeconds <= 0) intervalSeconds = 10;

        var timeoutSeconds = domainName.HealthCheckTimeoutSeconds;
        if (timeoutSeconds <= 0) timeoutSeconds = 3;

        if (timeoutSeconds > intervalSeconds) timeoutSeconds = intervalSeconds;

        return new HealthCheckConfig
        {
            Active = new ActiveHealthCheckConfig
            {
                Enabled = true,
                Interval = TimeSpan.FromSeconds(intervalSeconds),
                Timeout = TimeSpan.FromSeconds(timeoutSeconds),
                Policy = "ConsecutiveFailures",
                Path = path,
                Query = query
            },
            Passive = new PassiveHealthCheckConfig
            {
                Enabled = true,
                Policy = "TransportFailureRate",
                ReactivationPeriod = TimeSpan.FromSeconds(intervalSeconds)
            },
            AvailableDestinationsPolicy = HealthCheckConstants.AvailableDestinations.HealthyAndUnknown
        };
    }

    /// <summary>
    ///     默认的内容类型提供程序
    /// </summary>
    private static readonly DefaultContentTypeProvider DefaultContentTypeProvider = new();

    /// <summary>
    ///     证书缓存（按 SNI 名称及默认证书缓存 X509Certificate2 实例）
    /// </summary>
    private static readonly ConcurrentDictionary<string, X509Certificate2> CertificateCache = new();

    /// <summary>
    ///     证书缓存失效（根据域名）
    /// </summary>
    /// <param name="domain">SNI 域名</param>
    public static void InvalidateCertificate(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return;
        var normalized = domain.Trim().ToLowerInvariant();

        // 泛域名证书：清除所有匹配该泛域名的 SNI 缓存（如 *.example.com 对应 a.example.com、b.example.com 等）
        if (normalized.StartsWith("*."))
        {
            var suffix = normalized[1..]; // ".example.com"
            foreach (var key in CertificateCache.Keys)
            {
                if (!key.StartsWith("sni:")) continue;
                var host = key["sni:".Length..];
                if (host != normalized && !host.EndsWith(suffix)) continue;
                if (CertificateCache.TryRemove(key, out var wildcardCert))
                {
                    try { wildcardCert.Dispose(); } catch { /* ignore */ }
                }
            }

            return;
        }

        var exactKey = $"sni:{normalized}";
        if (CertificateCache.TryRemove(exactKey, out var cert))
        {
            try { cert.Dispose(); } catch { /* ignore */ }
        }
    }

    /// <summary>
    ///     是否存在80端口服务
    /// </summary>
    public static bool Has80Service { get; private set; }

    /// <summary>
    ///     检查Server是否在线
    /// </summary>
    /// <param name="serverId"></param>
    /// <returns></returns>
    public static bool CheckServerOnline(string serverId)
    {
        return GatewayWebApplications.ContainsKey(serverId);
    }

    public static ServerHealthDto GetServerHealth(string serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId))
            return new ServerHealthDto { Online = false };

        if (!GatewayWebApplications.TryGetValue(serverId, out var webApplication))
            return new ServerHealthDto { Online = false };

        var stateLookup = webApplication.Services.GetService<IProxyStateLookup>();
        if (stateLookup == null)
            return new ServerHealthDto { Online = true, Supported = false };

        var clusters = stateLookup
            .GetClusters()
            .Select(cluster =>
            {
                var healthCheck = cluster.Model.Config.HealthCheck;
                var active = healthCheck?.Active;
                var passive = healthCheck?.Passive;

                var enabled = active?.Enabled == true || passive?.Enabled == true;
                var path = active?.Path == null ? null : active.Path + (active.Query ?? string.Empty);

                return new ClusterHealthDto
                {
                    ClusterId = cluster.ClusterId,
                    HealthCheck = new ClusterHealthCheckDto
                    {
                        Enabled = enabled,
                        Path = path
                    },
                    Destinations = cluster.Destinations
                        .Select(kvp => new DestinationHealthDto
                        {
                            DestinationId = kvp.Value.DestinationId,
                            Address = kvp.Value.Model.Config.Address,
                            Health = new DestinationHealthStateDto
                            {
                                Active = FormatDestinationHealth(kvp.Value.Health.Active),
                                Passive = FormatDestinationHealth(kvp.Value.Health.Passive),
                                Effective = FormatDestinationHealth(GetEffectiveHealth(kvp.Value.Health))
                            }
                        })
                        .ToArray()
                };
            })
            .ToArray();

        return new ServerHealthDto
        {
            Online = true,
            Supported = true,
            CheckedAtUtc = DateTime.UtcNow,
            Clusters = clusters
        };
    }

    /// <summary>
    ///     展示用综合健康：任一维度 Unhealthy 即摘除；探测或代理曾判定 Healthy 则视为健康。
    ///     被动检查在无失败时保持 Unknown，不能因此把已探测成功的节点显示成未知。
    /// </summary>
    private static DestinationHealth GetEffectiveHealth(DestinationHealthState healthState)
    {
        if (healthState.Active == DestinationHealth.Unhealthy ||
            healthState.Passive == DestinationHealth.Unhealthy)
            return DestinationHealth.Unhealthy;

        if (healthState.Active == DestinationHealth.Healthy ||
            healthState.Passive == DestinationHealth.Healthy)
            return DestinationHealth.Healthy;

        return DestinationHealth.Unknown;
    }

    private static string FormatDestinationHealth(DestinationHealth health)
    {
        return health switch
        {
            DestinationHealth.Healthy => "Healthy",
            DestinationHealth.Unhealthy => "Unhealthy",
            _ => "Unknown"
        };
    }

    /// <summary>
    ///     重载路由
    /// </summary>
    /// <param name="server"></param>
    /// <param name="domainNames"></param>
    public static void ReloadGateway(Server server, List<DomainName> domainNames)
    {
        if (GatewayWebApplications.TryGetValue(server.Id, out var webApplication))
        {
            var inMemoryConfigProvider = webApplication.Services.GetRequiredService<InMemoryConfigProvider>();

            // 仅注入注册到本 Server 且控制连接在线的节点，断线即随下一次重载清理（避免死路由）
            foreach (var state in TunnelClientProxy.GetAllStates())
            {
                if (state.ServerId != server.Id || state.Client == null || state.Tunnel == null) continue;

                var node = TunnelNodeStore.GetNode(state.Tunnel.Name);
                if (node is not { Enabled: true }) continue;

                foreach (var proxy in state.Tunnel.Proxy)
                {
                    // 面板覆盖优先于客户端上报的启用状态
                    var overrideItem = node.ProxyOverrides.FirstOrDefault(o => o.ProxyId == proxy.Id);
                    var effectiveEnabled = overrideItem?.Enabled ?? proxy.Enabled;
                    if (!effectiveEnabled) continue;

                    domainNames.Add(new DomainName
                    {
                        Enable = true,
                        // 热重载必须稳定 cluster/route Id，否则 YARP 会拆掉旧 HttpClient 再建一份；
                        // proxy.Id 为内容哈希，跨注册保持稳定
                        Id = $"tunnel:{state.Tunnel.Name}:{proxy.Id}",
                        Path = proxy.Route,
                        Domains = proxy.Domains,
                        ServiceType = ServiceType.Service,
                        Service = $"http://node_{state.Tunnel.Name}{proxy.Route}",
                        Host = string.IsNullOrEmpty(proxy.Host) ? null : proxy.Host
                    });
                }
            }

            var (routes, clusters) = BuildConfig(domainNames.ToArray(), server);

            inMemoryConfigProvider.Update(routes, clusters);
        }
    }

    /// <summary>
    ///     关闭指定网关
    /// </summary>
    /// <param name="serverId"></param>
    /// <returns></returns>
    public static async Task<bool> CloseGateway(string serverId)
    {
        ClientIpHelper.RemoveClientIpSource(serverId);

        if (GatewayWebApplications.TryRemove(serverId, out var webApplication))
        {
            await webApplication.StopAsync();

            await webApplication.DisposeAsync();

            return true;
        }

        return false;
    }

    /// <summary>
    ///     找到证书（带缓存，避免每次握手都创建新实例）
    /// </summary>
    /// <param name="context"></param>
    /// <param name="name">SNI 主机名</param>
    /// <returns></returns>
    private static X509Certificate2 ServerCertificateSelector(ConnectionContext? context, string name)
    {
        // 规范化缓存键（可能为空）
        var sni = string.IsNullOrWhiteSpace(name) ? "__default__" : name.Trim().ToLowerInvariant();

        try
        {
            // 优先按 SNI 查找业务证书
            var certInfo = CertService.GetCert(name);
            if (certInfo != null)
            {
                return CertificateCache.GetOrAdd($"sni:{sni}", _ =>
                    new X509Certificate2(
                        certInfo.Certs.File,
                        certInfo.Certs.Password,
                        X509KeyStorageFlags.EphemeralKeySet));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        // 回退到默认证书
        var defaultKey = "default";
        try
        {
            return CertificateCache.GetOrAdd(defaultKey, _ =>
                new X509Certificate2(
                    Path.Combine(AppContext.BaseDirectory, "gateway.pfx"),
                    "010426",
                    X509KeyStorageFlags.EphemeralKeySet));
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            // 发生异常时，尽量最后再尝试无 flags 的构造，保持与旧实现兼容
            return new X509Certificate2(Path.Combine(AppContext.BaseDirectory, "gateway.pfx"), "010426");
        }
    }

    /// <summary>
    ///     构建网关
    /// </summary>
    public static async Task BuilderGateway(Server server, DomainName[] domainNames,
        List<BlacklistAndWhitelist> blacklistAndWhitelists, List<RateLimit> rateLimits)
    {
        try
        {
            if (!server.Enable) return;

            var is80 = server.Listen == 80;
            if (is80) Has80Service = true;

            var builder = WebApplication.CreateBuilder();

            builder.WebHost.UseKestrel(options =>
            {
                if (server.IsHttps)
                {
                    if (is80) options.Listen(IPAddress.Any, server.Listen);

                    options.Listen(IPAddress.Any, is80 ? 443 : server.Listen, listenOptions =>
                    {
                        Action<HttpsConnectionAdapterOptions> configure = adapterOptions =>
                        {
                            adapterOptions.ServerCertificateSelector = ServerCertificateSelector;
                        };

                        if (is80) listenOptions.UseHttps(configure);

                        listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                    });
                }
                else
                {
                    options.ListenAnyIP(server.Listen);
                }

                options.Limits.MaxRequestBodySize = server.MaxRequestBodySize;
            });

            builder.Services.Configure<FormOptions>(options =>
            {
                options.ValueLengthLimit = int.MaxValue;
                options.MultipartBodyLengthLimit = long.MaxValue;
                options.MultipartHeadersLengthLimit = int.MaxValue;
            });

            builder.Services.AddWebSockets(options =>
            {
                options.KeepAliveTimeout = TimeSpan.FromSeconds(120);
                options.AllowedOrigins.Add("*");
            });

            builder.WebHost.ConfigureKestrel((kestrel =>
            {
                kestrel.RequestHeaderEncodingSelector = _ => Encoding.UTF8;
                // and/or
                kestrel.ResponseHeaderEncodingSelector = _ => Encoding.UTF8;
                kestrel.Limits.MaxConcurrentConnections = server.MaxConcurrentConnections;
                kestrel.Limits.MaxConcurrentUpgradedConnections = server.MaxConcurrentUpgradedConnections;
                kestrel.AddServerHeader = false;
            }));

            builder.Services
                .AddCors(options =>
                {
                    options.AddPolicy("AllowAll",
                        builder => builder
                            .SetIsOriginAllowed(_ => true)
                            .AllowAnyMethod()
                            .AllowAnyHeader()
                            .AllowCredentials());
                });

            var (routes, clusters) = BuildConfig(domainNames, server);

            builder.Services.AddTunnel();
            // 供 AgentManagerMiddleware 获知节点连接所属的网关 Server（断线清理路由需要）
            builder.Services.AddSingleton(server);
            builder.Services.AddSingleton<FailoverHttpClientPool>();
            builder.Services.AddSingleton<StandardForwarderHttpClientFactory>();
            builder.Services.AddSingleton<FastGatewayForwarderHttpClientFactory>();
            builder.Services.AddSingleton<IForwarderHttpClientFactory>(s => s.GetRequiredService<FastGatewayForwarderHttpClientFactory>());
            builder.Services.AddSingleton<ConfigurationService>();
            builder.Services.AddBotProtection(builder.Configuration);

            if (server.StaticCompress)
                builder.Services.AddResponseCompression();

            // 默认请求超时兜底：框架默认 100s 会把耗时较长的普通响应（大文件、慢上游、
            // stream:true 但 Accept 非 SSE 的 AI 流式回答）在传输途中切断，客户端表现为
            // net_http_invalid_response_premature_eof。放宽到 10 分钟；真正的流式请求另行 DisableTimeout。
            builder.Services.AddRequestTimeouts(options =>
            {
                options.DefaultPolicy = new RequestTimeoutPolicy
                {
                    Timeout = TimeSpan.FromMinutes(10)
                };
            });

            var gatewayVersion = typeof(Gateway).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? typeof(Gateway).Assembly.GetName().Version?.ToString()
                ?? "unknown";

            builder.Services
                .AddReverseProxy()
                .LoadFromMemory(routes, clusters)
                .AddTransforms(context =>
                {
                    var prefix = context.Route.Match.Path?
                        .Replace("/{**catch-all}", "")
                        .Replace("{**catch-all}", "");

                    string? relayTargetNode = null;
                    var isRelay = context.Cluster?.Metadata?
                        .TryGetValue(ClusterRelay.RelayMetadataKey, out relayTargetNode) == true;

                    // 中继必须保留原始 Host（目标节点按域名匹配同一路由）；泛域名/CopyRequestHost 同理
                    if (isRelay
                        || context.Route.Match.Hosts?.Any(x => x.Contains('*')) == true
                        || server.CopyRequestHost)
                        context.AddOriginalHost();

                    // 中继保留完整路径，由目标节点的同一路由自行剥前缀
                    if (!isRelay && !string.IsNullOrEmpty(prefix)) context.AddPathRemovePrefix(prefix);

                    if (isRelay)
                        // 出方向：跳数 +1 并出示集群令牌，供下一跳节点信任与防环
                        context.AddRequestTransform(transformContext =>
                        {
                            var hops = 0;
                            var incoming = transformContext.HttpContext.Request.Headers[ClusterRelay.RelayCountHeader];
                            if (incoming.Count > 0) _ = int.TryParse(incoming[0], out hops);

                            var proxyHeaders = transformContext.ProxyRequest.Headers;
                            RemoveInternalRequestHeaders(proxyHeaders);
                            proxyHeaders.TryAddWithoutValidation(ClusterRelay.RelayCountHeader,
                                (hops + 1).ToString());

                            var token = ClusterRelay.GetRelayToken(relayTargetNode!);
                            if (token != null)
                                proxyHeaders.TryAddWithoutValidation(ClusterRelay.RelayTokenHeader, token);

                            return ValueTask.CompletedTask;
                        });
                    else
                        // 直连真实上游前剥离集群内部头，避免泄漏到业务服务
                        context.AddRequestTransform(transformContext =>
                        {
                            RemoveInternalRequestHeaders(transformContext.ProxyRequest.Headers);
                            return ValueTask.CompletedTask;
                        });

                    context.ResponseTransforms.Add(new ResponseFuncTransform((transformContext =>
                    {
                        var headers = transformContext.HttpContext.Response.Headers;
                        headers.Remove("Server");
                        headers["Server"] = "FastGateway";
                        headers[GatewayVersionHeader] = gatewayVersion;

                        return ValueTask.CompletedTask;
                    })));

                    #region 静态站点

                    if (context.Route.Metadata!.TryGetValue(Root, out var root))
                    {
                        var tryFiles = context.Cluster!.Metadata!.Select(p => p.Key).ToArray();
                        context.AddRequestTransform(async transformContext =>
                        {
                            var httpContext = transformContext.HttpContext;
                            var response = httpContext.Response;

                            // FileInfo 单次 stat 同时拿到存在性/长度/修改时间，替代 File.Exists + 打开文件的两次系统调用
                            var file = new FileInfo(Path.Combine(root, transformContext.Path.Value![1..]));

                            if (!file.Exists)
                            {
                                foreach (var tryFile in tryFiles)
                                {
                                    var candidate = new FileInfo(Path.Combine(root, tryFile));
                                    if (!candidate.Exists) continue;
                                    file = candidate;
                                    break;
                                }
                            }

                            if (!file.Exists)
                            {
                                response.StatusCode = 404;
                                return;
                            }

                            // HTTP 日期只有秒级精度，截断后再参与 ETag/If-Modified-Since 比较
                            var lastModifiedTicks = file.LastWriteTimeUtc.Ticks;
                            lastModifiedTicks -= lastModifiedTicks % TimeSpan.TicksPerSecond;
                            var lastModified = new DateTimeOffset(lastModifiedTicks, TimeSpan.Zero);
                            var etag = $"\"{lastModifiedTicks:x}-{file.Length:x}\"";

                            var headers = response.Headers;
                            headers.ETag = etag;
                            headers.LastModified = lastModified.ToString("R");
                            // 强制协商缓存：浏览器可缓存但每次需验证，命中返回 304 省掉响应体传输
                            headers.CacheControl = "public, max-age=0, must-revalidate";

                            var request = httpContext.Request;
                            if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
                            {
                                var ifNoneMatch = request.Headers.IfNoneMatch;
                                if (ifNoneMatch.Count > 0)
                                {
                                    if (IsEtagMatch(ifNoneMatch, etag))
                                    {
                                        response.StatusCode = StatusCodes.Status304NotModified;
                                        return;
                                    }
                                }
                                else if (request.Headers.IfModifiedSince.Count > 0 &&
                                         DateTimeOffset.TryParse(request.Headers.IfModifiedSince.ToString(),
                                             out var ifModifiedSince) &&
                                         lastModified <= ifModifiedSince)
                                {
                                    response.StatusCode = StatusCodes.Status304NotModified;
                                    return;
                                }
                            }

                            DefaultContentTypeProvider.TryGetContentType(file.FullName, out var contentType);
                            headers.ContentType = contentType;
                            response.ContentLength = file.Length;

                            if (HttpMethods.IsHead(request.Method)) return;

                            await response.SendFileAsync(file.FullName);
                        });
                    }

                    #endregion
                }); // 删除 ConfigureHttpClient 避免与自定义工厂冲突

            var app = builder.Build();

            app.UseCors("AllowAll");

            app.UseWebSockets();

            // 压缩只对静态文件路由生效：代理转发的动态内容透传上游的 Content-Encoding，
            // 避免网关为上游未压缩的大响应白白消耗 CPU（WebApplication 会在用户中间件前
            // 自动插入路由中间件，此处能拿到 YARP 路由端点的元数据）
            if (server.StaticCompress)
                app.UseWhen(
                    ctx => ctx.GetEndpoint()?.Metadata.GetMetadata<RouteModel>()?.Config.Metadata
                        ?.ContainsKey(Root) == true,
                    branch => branch.UseResponseCompression());

            if (is80)
                // 用于HTTPS证书签名校验
                app.Use(async (context, next) =>
                {
                    if (context.Request.Path.StartsWithSegments("/.well-known/acme-challenge", out var token))
                        await CertService.Challenge(context, token.Value![1..]);
                    else
                        await next.Invoke();
                });

            // HTTPS 重定向：将 80 端口的明文请求重定向到 HTTPS
            // 放在 ACME 校验之后，避免影响 HTTP-01 证书签发
            // 若前置反向代理已通过 X-Forwarded-Proto=https 声明客户端协议为 HTTPS，则不再跳转
            if (is80 && server is { IsHttps: true, RedirectHttps: true })
                app.Use(async (context, next) =>
                {
                    if (!context.Request.IsHttps && !IsForwardedHttps(context.Request))
                    {
                        var host = context.Request.Host.Host;
                        var location =
                            $"https://{host}{context.Request.PathBase}{context.Request.Path}{context.Request.QueryString}";
                        context.Response.StatusCode = StatusCodes.Status307TemporaryRedirect;
                        context.Response.Headers.Location = location;
                        return;
                    }

                    await next.Invoke();
                });

            app.UseClientIpResolution(server.Id, server.ClientIpSource);
            app.UseInitGatewayMiddleware();

            // 统计采集：在限流/黑名单之前挂载，next 之后记录，可观察到 403/429 短路后的最终状态
            app.UseStatisticsCapture(server.Id);

            app.UseRequestTimeouts();
            app.Use(async (context, next) =>
            {
                var timeoutFeature = context.Features.Get<IHttpRequestTimeoutFeature>();

                if (IsSseRequest(context.Request))
                {
                    // 请求阶段已能判定为 SSE：直接豁免
                    timeoutFeature?.DisableTimeout();
                    context.Response.Headers.CacheControl = "no-cache";
                    context.Response.Headers["X-Accel-Buffering"] = "no";
                }
                else if (timeoutFeature != null)
                {
                    // 请求阶段无法判定（如 AI 流式：Accept 非 SSE，靠 body stream:true 触发）。
                    // 挂响应回调：一旦上游返回 text/event-stream，在写出响应体前豁免超时，
                    // 避免长回答传输途中被请求超时切断（premature_eof）。
                    context.Response.OnStarting(state =>
                    {
                        var ctx = (HttpContext)state;
                        if (IsStreamingResponse(ctx.Response))
                        {
                            ctx.Features.Get<IHttpRequestTimeoutFeature>()?.DisableTimeout();
                            ctx.Response.Headers.CacheControl = "no-cache";
                            ctx.Response.Headers["X-Accel-Buffering"] = "no";
                        }

                        return Task.CompletedTask;
                    }, context);
                }

                await next(context);
            });

            app.UseRateLimitMiddleware(rateLimits);

            // 黑名单默认启用（安全防护），白名单按服务开关控制
            app.UseBlacklistMiddleware(blacklistAndWhitelists, enableBlacklist: true, enableWhitelist: server.EnableWhitelist);
            app.UseRiskGuard();
            app.UseBotProtection();

            app.UseClusterRequestFailover(server.Id, gatewayVersion);
            app.UseProxyErrorResponse(gatewayVersion);

            app.UseAbnormalIpMonitoring(server.Id);

            GatewayWebApplications.TryAdd(server.Id, app);

            // 隧道接入端点仅在 Server 开启隧道时挂载，使 EnableTunnel 语义真正生效
            if (server.EnableTunnel)
            {
                app.Use(async (context, next) =>
                {
                    if (context.Request.Path == "/internal/gateway/Server/register")
                    {
                        var dto = await context.Request.ReadFromJsonAsync(AppJsonContext.Default.Tunnel);
                        if (dto == null || string.IsNullOrWhiteSpace(dto.Name))
                        {
                            context.Response.StatusCode = StatusCodes.Status400BadRequest;
                            await context.Response.WriteAsJsonAsync(
                                new CodeMessageDto { Code = 400, Message = "请求数据格式错误" },
                                AppJsonContext.Default.CodeMessageDto);
                            return;
                        }

                        // 节点级密钥认证；全局 TunnelToken 兼容旧客户端并自动建档
                        var credential = TunnelNodeStore.ExtractCredential(context)
                                         ?? dto.Token;
                        var auth = TunnelNodeStore.Authenticate(dto.Name, credential);
                        if (!auth.Success)
                        {
                            context.Response.StatusCode = auth.StatusCode;
                            await context.Response.WriteAsJsonAsync(
                                new CodeMessageDto { Code = auth.StatusCode, Message = auth.Message },
                                AppJsonContext.Default.CodeMessageDto);
                            return;
                        }

                        TunnelClientProxy.Register(dto, server);
                        context.Response.StatusCode = StatusCodes.Status200OK;
                        await context.Response.WriteAsJsonAsync(
                            new CodeMessageDto { Code = 200, Message = "注册成功" },
                            AppJsonContext.Default.CodeMessageDto);
                        return;
                    }

                    await next(context);
                });
                app.Map("/internal/gateway/Server", builder =>
                {
                    builder.UseMiddleware<AgentManagerMiddleware>();
                    builder.UseMiddleware<AgentManagerTunnelMiddleware>();
                });
            }

            app.MapBotProtectionEndpoints();
            app.MapRiskGuardEndpoints();
            app.MapReverseProxy();

            app.Lifetime.ApplicationStopping.Register(() => { GatewayWebApplications.Remove(server.Id, out _); });

            await app.RunAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine("网关启动错误：" + e);
            throw;
        }
        finally
        {
            GatewayWebApplications.Remove(server.Id, out _);
        }
    }

    /// <summary>
    ///     Alt-Svc 头按端口预生成（每网关端口固定，避免每请求字符串拼接）
    /// </summary>
    private static readonly ConcurrentDictionary<int, string> AltSvcCache = new();

    private static void RemoveInternalRequestHeaders(HttpRequestHeaders proxyHeaders)
    {
        proxyHeaders.Remove(ClusterRelay.RelayCountHeader);
        proxyHeaders.Remove(ClusterRelay.RelayTokenHeader);

        if (!proxyHeaders.TryGetValues("Cookie", out var cookieValues)) return;

        var retainedCookies = new List<string>();
        foreach (var cookieHeader in cookieValues)
        {
            foreach (var cookie in cookieHeader.Split(';'))
            {
                var separator = cookie.IndexOf('=');
                var name = (separator >= 0 ? cookie[..separator] : cookie).Trim();
                if (name.Length == 0 ||
                    string.Equals(name, BotProtectionService.ClearanceCookieName, StringComparison.Ordinal) ||
                    string.Equals(name, RiskGuardMiddleware.ClearanceCookieName, StringComparison.Ordinal))
                    continue;

                retainedCookies.Add(cookie.Trim());
            }
        }

        proxyHeaders.Remove("Cookie");
        if (retainedCookies.Count > 0)
            proxyHeaders.TryAddWithoutValidation("Cookie", string.Join("; ", retainedCookies));
    }

    private static WebApplication UseInitGatewayMiddleware(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var ip = context.Connection.RemoteIpAddress;
            var requestHeaders = context.Request.Headers;

            // 集群中继识别：令牌有效才信任跳数头，否则视为外部伪造并清除
            var trustedRelay = false;
            if (requestHeaders.ContainsKey(ClusterRelay.RelayCountHeader))
            {
                if (ClusterRelay.ValidateRelayToken(requestHeaders[ClusterRelay.RelayTokenHeader]))
                {
                    trustedRelay = true;

                    if (int.TryParse(requestHeaders[ClusterRelay.RelayCountHeader], out var hops)
                        && hops > ClusterRelay.MaxHops)
                    {
                        // 正常链路最多两跳（Worker → Master → Worker），超出说明节点间配置不一致成环
                        context.Response.StatusCode = StatusCodes.Status508LoopDetected;
                        return;
                    }
                }
                else
                {
                    requestHeaders.Remove(ClusterRelay.RelayCountHeader);
                    requestHeaders.Remove(ClusterRelay.RelayTokenHeader);
                }
            }

            // 覆写 X-Forwarded-For 为直连 IP：防止客户端伪造的 XFF 链透传给上游；
            // 可信中继保留上一跳网关写入的真实客户端 IP
            if (!trustedRelay)
                requestHeaders["X-Forwarded-For"] = ip?.ToString();

            if (context.Request.IsHttps)
            {
                // h3 需要对应请求的端口；端口种类极少，按端口缓存完整头值
                var port = context.Request.Host.Port ?? 443;
                context.Response.Headers.AltSvc =
                    AltSvcCache.GetOrAdd(port, static p => $"h3=\":{p}\"");
            }

            await next(context);

            QpsService.IncrementServiceRequests();
        });

        return app;
    }

    /// <summary>
    ///     If-None-Match 匹配：支持多值与 "*"，弱校验前缀 W/ 一并比对
    /// </summary>
    private static bool IsEtagMatch(StringValues ifNoneMatch, string etag)
    {
        for (var i = 0; i < ifNoneMatch.Count; i++)
        {
            var value = ifNoneMatch[i];
            if (string.IsNullOrEmpty(value)) continue;
            if (value == "*") return true;

            var span = value.AsSpan();
            foreach (var range in span.Split(','))
            {
                var candidate = span[range].Trim();
                if (candidate.StartsWith("W/", StringComparison.Ordinal)) candidate = candidate[2..];
                if (candidate.SequenceEqual(etag)) return true;
            }
        }

        return false;
    }

    private static ForwarderRequestConfig CreateHttpRequestConfig(Server server, bool isTunnel)
    {
        var timeoutSeconds = server.Timeout > 0 ? server.Timeout : 900;
        if (timeoutSeconds < 600) timeoutSeconds = 600;

        // 与 v2.14.0 对齐：默认 Version=2.0 + OrLower。进程未开 Http2UnencryptedSupport 时，
        // 明文 http:// 上游由 .NET 静默降级为 HTTP/1.1（不做 h2c 探测），
        // HTTPS 上游经 ALPN 协商 HTTP/2 获得多路复用。
        // 隧道对端按 h2c 设计，用 RequestVersionExact 显式启用明文 HTTP/2。
        return new ForwarderRequestConfig
        {
            ActivityTimeout = TimeSpan.FromSeconds(timeoutSeconds),
            AllowResponseBuffering = false,
            Version = HttpVersion.Version20,
            VersionPolicy = isTunnel
                ? HttpVersionPolicy.RequestVersionExact
                : HttpVersionPolicy.RequestVersionOrLower
        };
    }

    private static Dictionary<string, string> CreateClusterMetadata(string? service)
    {
        return new Dictionary<string, string>(1)
        {
            {
                ClientModeMetadataKey,
                IsTunnelService(service) ? TunnelClientMode : StandardClientMode
            }
        };
    }

    /// <summary>中继 cluster 元数据：传输模式（隧道/标准）+ 中继标记（值为目标节点 Id）</summary>
    private static Dictionary<string, string> CreateRelayClusterMetadata(string relayAddress, string accessNodeId)
    {
        return new Dictionary<string, string>(2)
        {
            {
                ClientModeMetadataKey,
                IsTunnelService(relayAddress) ? TunnelClientMode : StandardClientMode
            },
            { ClusterRelay.RelayMetadataKey, accessNodeId }
        };
    }

    private static bool IsTunnelService(string? service)
    {
        if (string.IsNullOrWhiteSpace(service)) return false;
        return service.StartsWith("http://node_", StringComparison.OrdinalIgnoreCase)
               || service.StartsWith("https://node_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSseRequest(HttpRequest request)
    {
        // 标准 SSE：显式 Accept: text/event-stream
        if (request.Headers.Accept.ToString().Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
            return true;

        // AI/LLM 流式补全：很多客户端（Anthropic/OpenAI SDK、各类中转）用 Accept: application/json + 请求体
        // "stream": true 触发流式响应，Accept 头并非 text/event-stream。这类响应同样是长连接 SSE，
        // 若不豁免超时会在 100s（或默认策略）处被切断，客户端报 premature_eof。
        // 不读取/缓冲请求体（会破坏转发），仅按方法+Content-Type 粗筛，真正判定交由响应阶段的 Content-Type。
        return false;
    }

    /// <summary>
    ///     响应阶段判定：上游是否以流式（SSE）返回。用于在响应头就绪后二次豁免请求超时。
    /// </summary>
    private static bool IsStreamingResponse(HttpResponse response)
    {
        var contentType = response.ContentType;
        return !string.IsNullOrEmpty(contentType) &&
               contentType.Contains("text/event-stream", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     判断请求是否经反向代理以 HTTPS 接入（依据 X-Forwarded-Proto）。
    /// </summary>
    private static bool IsForwardedHttps(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto))
            return false;

        var value = forwardedProto.ToString();
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // 可能是 "https" 或 "https,http" 等多值，取第一个
        var separatorIndex = value.IndexOf(',');
        var proto = separatorIndex >= 0 ? value.AsSpan(0, separatorIndex) : value.AsSpan();
        return proto.Trim().Equals("https", StringComparison.OrdinalIgnoreCase);
    }

    private static (IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) BuildConfig(
        DomainName[] domainNames, Server server)
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        foreach (var domainName in domainNames)
        {
            // 路由 Id 即域名 Id，风控中间件按 RouteId 查找；中继路由同样在第一跳执行风控
            RiskEngine.Shared.SetRouteProtection(domainName.Id, domainName.RiskProtection);

            var routePath = domainName.Path?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(routePath) || routePath == "/")
                routePath = "/";
            else
                routePath = $"/{routePath.Trim('/') }";

            var path = routePath == "/"
                ? "/{**catch-all}"
                : $"{routePath}/{{**catch-all}}";

            // 集群「访问节点」中继：路由指定了其他节点访问上游时，本节点仅做二跳转发
            // （Worker → Master 直连回源；Master → Worker 走集群隧道），保留原始 Host 与完整路径
            var relayAddress = ClusterRelay.ResolveRelayAddress(domainName.AccessNodeId, server);
            if (relayAddress != null)
            {
                routes.Add(new RouteConfig
                {
                    RouteId = domainName.Id,
                    ClusterId = domainName.Id,
                    Timeout = TimeSpan.FromSeconds(server.Timeout),
                    Match = new RouteMatch
                    {
                        Hosts = domainName.Domains,
                        Path = path
                    },
                    Metadata = BotProtectionService.CreateRouteMetadata(domainName, routePath)
                });

                clusters.Add(new ClusterConfig
                {
                    ClusterId = domainName.Id,
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        { "relay", new DestinationConfig { Address = relayAddress } }
                    },
                    // 主动健康检查探测的是中继链路而非真实上游，对中继目的地不启用
                    HttpRequest = CreateHttpRequestConfig(server, IsTunnelService(relayAddress)),
                    Metadata = CreateRelayClusterMetadata(relayAddress, domainName.AccessNodeId!)
                });

                continue;
            }

            Dictionary<string, string> routeMetadata, clusterMetadata;

            if (domainName.ServiceType == ServiceType.StaticFile)
            {
                routeMetadata = new Dictionary<string, string>(1) { { Root, domainName.Root! } };

                clusterMetadata = new Dictionary<string, string>(domainName.TryFiles!.Length);
                foreach (var item in domainName.TryFiles) clusterMetadata.Add(item, string.Empty);
            }
            else
            {
                routeMetadata = clusterMetadata = new Dictionary<string, string>(0);
            }

            var route = new RouteConfig
            {
                RouteId = domainName.Id,
                ClusterId = domainName.Id,
                Timeout = TimeSpan.FromSeconds(server.Timeout),
                Match = new RouteMatch
                {
                    Hosts = domainName.Domains,
                    Path = path
                },
                Metadata = BotProtectionService.CreateRouteMetadata(domainName, routePath, routeMetadata)
            };

            // 隧道目的地需保留访客原始 Host：客户端本地 YARP 按域名匹配代理规则，
            // 若被改写成 node_{name} 会全部 404。显式配置 Host 的除外（由目的地 Host 覆盖）。
            if (IsTunnelService(domainName.Service) && string.IsNullOrEmpty(domainName.Host))
                route = route with
                {
                    Metadata = BotProtectionService.CreateRouteMetadata(domainName, routePath, route.Metadata),
                    Transforms =
                    [
                        new Dictionary<string, string> { ["RequestHeaderOriginalHost"] = "true" }
                    ]
                };

            if (domainName.ServiceType == ServiceType.Service)
            {
                DestinationConfig config;

                if (!string.IsNullOrEmpty(domainName.Host))
                    config = new DestinationConfig
                    {
                        Address = domainName.Service,
                        Host = domainName.Host
                    };
                else
                    config = new DestinationConfig
                    {
                        Address = domainName.Service
                    };

                var cluster = new ClusterConfig
                {
                    ClusterId = domainName.Id,
                    // destination key 必须在配置重载间保持稳定，否则 YARP 会当作删旧建新，
                    // 清空健康检查状态与负载均衡计数
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        {
                            "default",
                            config
                        }
                    },
                    HealthCheck = CreateHealthCheckConfig(domainName),
                    HttpRequest = CreateHttpRequestConfig(server, IsTunnelService(domainName.Service)),
                    Metadata = CreateClusterMetadata(domainName.Service)
                };

                clusters.Add(cluster);
            }

            if (domainName.ServiceType == ServiceType.ServiceCluster)
            {
                var destinations = new Dictionary<string, DestinationConfig>(domainName.UpStreams.Count);
                foreach (var upStream in domainName.UpStreams)
                {
                    // 以上游地址作稳定 key，重复地址追加序号兜底
                    var key = destinations.ContainsKey(upStream.Service)
                        ? $"{upStream.Service}#{destinations.Count}"
                        : upStream.Service;
                    destinations.Add(key, new DestinationConfig
                    {
                        Address = upStream.Service
                    });
                }

                var cluster = new ClusterConfig
                {
                    ClusterId = domainName.Id,
                    Destinations = destinations,
                    LoadBalancingPolicy = LoadBalancingPolicies.LeastRequests,
                    HealthCheck = CreateHealthCheckConfig(domainName),
                    HttpRequest = CreateHttpRequestConfig(server,
                        domainName.UpStreams.Count > 0
                        && domainName.UpStreams.TrueForAll(u => IsTunnelService(u.Service))),
                    Metadata = CreateClusterMetadata(null)
                };

                clusters.Add(cluster);
            }

            if (domainName.ServiceType == ServiceType.StaticFile)
            {
                var cluster = new ClusterConfig
                {
                    ClusterId = domainName.Id,
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        {
                            "static",
                            StaticProxyDestination
                        }
                    },
                    Metadata = clusterMetadata
                };

                clusters.Add(cluster);
            }

            routes.Add(route);
        }


        return (routes, clusters);
    }
}
