using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Core.Entities;
using Core.Entities.Core;
using FastGateway.Extensions;
using FastGateway.Infrastructure;
using FastGateway.Options;
using FastGateway.Services;
using FastGateway.Tunnels;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.WebSockets;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Transforms;

namespace FastGateway.Gateway;

/// <summary>
///     网关服务
/// </summary>
public static class Gateway
{
    private const string Root = "Root";
    private static readonly ConcurrentDictionary<string, WebApplication> GatewayWebApplications = new();

    private static readonly DestinationConfig StaticProxyDestination = new() { Address = "http://127.0.0.1" };

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
        var key = $"sni:{domain.Trim().ToLowerInvariant()}";
        if (CertificateCache.TryRemove(key, out var cert))
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

            var tunnels = TunnelClientProxy.GetAllClients();


            if (tunnels is { Count: > 0 })
                foreach (var tunnel in tunnels)
                    foreach (var proxy in tunnel.Proxy)
                    {
                        var value = new DomainName
                        {
                            Enable = proxy.Enabled,
                            Id = Guid.NewGuid().ToString(),
                            Path = proxy.Route,
                            Domains = proxy.Domains,
                            ServiceType = ServiceType.Service,
                            Service = $"http://node_{tunnel.Name}{proxy.Route}"
                        };

                        if (!string.IsNullOrEmpty(proxy.Host)) value.Host = proxy.Host;
                        domainNames.Add(value);
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

            builder.Services.AddRateLimitService(rateLimits);

            builder.Services.AddTunnel();

            if (server.StaticCompress)
                builder.Services.AddResponseCompression();

            builder.Services.AddRequestTimeouts();

            builder.Services
                .AddReverseProxy()
                .LoadFromMemory(routes, clusters)
                .AddTransforms(context =>
                {
                    var prefix = context.Route.Match.Path?
                        .Replace("/{**catch-all}", "")
                        .Replace("{**catch-all}", "");

                    // 如果存在泛域名则需要保留原始Host
                    if (context.Route.Match.Hosts?.Any(x => x.Contains('*')) == true)
                        context.AddOriginalHost();
                    else if (server.CopyRequestHost) context.AddOriginalHost();

                    if (!string.IsNullOrEmpty(prefix)) context.AddPathRemovePrefix(prefix);

                    #region 静态站点

                    if (context.Route.Metadata!.TryGetValue(Root, out var root))
                    {
                        var tryFiles = context.Cluster!.Metadata!.Select(p => p.Key).ToArray();
                        context.AddRequestTransform(async transformContext =>
                        {
                            var response = transformContext.HttpContext.Response;
                            var path = Path.Combine(root, transformContext.Path.Value![1..]);

                            if (File.Exists(path))
                            {
                                DefaultContentTypeProvider.TryGetContentType(path, out var contentType);
                                response.Headers.ContentType = contentType;

                                await response.SendFileAsync(path);
                                return;
                            }

                            // 搜索 try_files
                            foreach (var tryFile in tryFiles)
                            {
                                var tryPath = Path.Combine(root, tryFile);

                                if (!File.Exists(tryPath)) continue;

                                DefaultContentTypeProvider.TryGetContentType(tryPath, out var contentType);
                                response.Headers.ContentType = contentType;

                                await response.SendFileAsync(tryPath);

                                return;
                            }

                            response.StatusCode = 404;
                        });
                    }

                    #endregion
                }); // 删除 ConfigureHttpClient 避免与自定义工厂冲突

            var app = builder.Build();

            app.UseCors("AllowAll");

            app.UseWebSockets();

            if (server.StaticCompress)
                app.UseResponseCompression();

            if (is80)
                // 用于HTTPS证书签名校验
                app.Use(async (context, next) =>
                {
                    if (context.Request.Path.StartsWithSegments("/.well-known/acme-challenge", out var token))
                        await CertService.Challenge(context, token.Value![1..]);
                    else
                        await next.Invoke();
                });

            app.UseInitGatewayMiddleware();

            app.UseRequestTimeouts();

            app.UseRateLimitMiddleware(rateLimits);

            if (server.EnableBlacklist || server.EnableWhitelist) app.UseBlacklistMiddleware(blacklistAndWhitelists);

            GatewayWebApplications.TryAdd(server.Id, app);

            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/internal/gateway/Server/register")
                {
                    var tunnel = context.RequestServices.GetRequiredService<TunnelClientProxy>();
                    var token = context.Request.Query["token"].ToString();
                    if (string.IsNullOrEmpty(token) || FastGatewayOptions.TunnelToken != token)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            Code = 401,
                            Message = "未授权的请求"
                        });
                        return;
                    }

                    var dto = await context.Request.ReadFromJsonAsync<Tunnel>();
                    if (dto == null)
                    {
                        context.Response.StatusCode = StatusCodes.Status400BadRequest;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            Code = 400,
                            Message = "请求数据格式错误"
                        });
                        return;
                    }

                    tunnel.CreateClient(dto, server, domainNames);
                    context.Response.StatusCode = StatusCodes.Status200OK;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        Code = 200,
                        Message = "注册成功"
                    });
                    return;
                }


                await next(context);
            });
            app.Map("/internal/gateway/Server", builder =>
            {
                builder.UseMiddleware<AgentManagerMiddleware>();
                builder.UseMiddleware<AgentManagerTunnelMiddleware>();
            });

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

    private static WebApplication UseInitGatewayMiddleware(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            // 设置ip
            var ip = context.Connection.RemoteIpAddress;
            if (!context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
                context.Request.Headers["X-Forwarded-For"] = ip?.ToString();

            if (context.Request.IsHttps)
                // TODO: 由于h3需要对应请求的端口，所以这里需要动态设置
                context.Response.Headers.AltSvc = "h3=\":" + (context.Request.Host.Port ?? 443) + "\"";

            await next(context);

            QpsService.IncrementServiceRequests();
        });

        return app;
    }

    private static (IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) BuildConfig(
        DomainName[] domainNames, Server server)
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        foreach (var domainName in domainNames)
        {
            var path = domainName.Path ?? string.Empty;
            if (string.IsNullOrWhiteSpace(path) || path == "/")
                path = "/{**catch-all}";
            else
                path = $"/{path.TrimStart('/')}/{{**catch-all}}";


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
                Metadata = routeMetadata
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
                    Destinations = new Dictionary<string, DestinationConfig>
                    {
                        {
                            Guid.NewGuid().ToString("N"),
                            config
                        }
                    }
                };

                clusters.Add(cluster);
            }

            if (domainName.ServiceType == ServiceType.ServiceCluster)
            {
                var destinations = domainName.UpStreams.Select(x => new DestinationConfig
                {
                    Address = x.Service
                }).ToDictionary(x => Guid.NewGuid().ToString("N"));

                var cluster = new ClusterConfig
                {
                    ClusterId = domainName.Id,
                    Destinations = destinations
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
                            Guid.NewGuid().ToString("N"),
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
