using System.Collections.Concurrent;
using System.Net;
using FastGateway.Entities;
using FastGateway.Entities.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Yarp.ReverseProxy.Configuration;

namespace FastGateway.Service.Gateway;

/// <summary>
/// 网关服务
/// </summary>
public static class Gateway
{
    private static readonly ConcurrentDictionary<string, WebApplication> GatewayWebApplications = new();

    /// <summary>
    /// 检查Server是否在线
    /// </summary>
    /// <param name="serverId"></param>
    /// <returns></returns>
    public static bool CheckServerOnline(string serverId)
    {
        return GatewayWebApplications.ContainsKey(serverId);
    }

    /// <summary>
    /// 是否存在80端口服务
    /// </summary>
    public static bool Has80Service { get; private set; }

    /// <summary>
    /// 构建网关
    /// </summary>
    public static async Task BuilderGateway(Server server, List<DomainName> domainNames)
    {
        try
        {
            if (!server.Enable)
            {
                return;
            }

            bool is80 = server.Listen == 80;
            if (is80)
            {
                Has80Service = true;
            }

            var builder = WebApplication.CreateBuilder();

            builder.WebHost.UseKestrel(options =>
            {
                if (server.IsHttps)
                {
                    if (is80)
                    {
                        options.Listen(IPAddress.Any, server.Listen);
                    }

                    options.Listen(IPAddress.Any, is80 ? 443 : server.Listen, listenOptions =>
                    {
                        Action<HttpsConnectionAdapterOptions> configure = adapterOptions =>
                        {
                            // adapterOptions.ServerCertificateSelector = (context, name) =>
                            //     CertService.Certs.TryGetValue(name, out var cert)
                            //         ? new X509Certificate2(cert.File, cert.Password)
                            //         : new X509Certificate2(Path.Combine(AppContext.BaseDirectory, "gateway.pfx"),
                            //             "010426");
                        };

                        if (is80)
                        {
                            listenOptions.UseHttps(configure);
                        }

                        listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                    });
                }
                else
                {
                    options.Listen(IPAddress.Any, server.Listen);
                }

                options.Limits.MaxRequestBodySize = null;
            });

            var (routes, clusters) = BuildConfig(domainNames);

            builder.Services.AddReverseProxy()
                .LoadFromMemory(routes, clusters);

            var app = builder.Build();

            GatewayWebApplications.TryAdd(server.Id, app);

            app.MapReverseProxy();

            app.Lifetime.ApplicationStopping.Register(() => { GatewayWebApplications.Remove(server.Id, out _); });

            await app.RunAsync();
        }
        finally
        {
            GatewayWebApplications.Remove(server.Id, out _);
        }
    }

    public static (IReadOnlyList<RouteConfig> routes,
        IReadOnlyList<ClusterConfig> clusters) BuildConfig(List<DomainName> domainNames)
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();

        foreach (var domainName in domainNames)
        {
            if (string.IsNullOrWhiteSpace(domainName.Path))
            {
                domainName.Path = "/{**catch-all}";
            }
            else if (domainName.Path == "/")
            {
                domainName.Path = $"/{{**catch-all}}";
            }
            else
            {
                domainName.Path = $"/{domainName.Path}/{{**catch-all}}";
            }

            RouteConfig route ;

            if (domainName.ServiceType == ServiceType.StaticFile)
            {
                route = new RouteConfig
                {
                    RouteId = domainName.Id,
                    ClusterId = domainName.Id,
                    Match = new RouteMatch
                    {
                        Hosts = domainName.Domains,
                        Path = domainName.Path,
                    },
                    
                };
            }
            else
            {
                route = new RouteConfig
                {
                    RouteId = domainName.Id,
                    ClusterId = domainName.Id,
                    Match = new RouteMatch
                    {
                        Hosts = domainName.Domains,
                        Path = domainName.Path,
                    }
                };
            }
            
            if (domainName.ServiceType == ServiceType.Service)
            {
                var cluster = new ClusterConfig
                {
                    ClusterId = domainName.Id,
                    Destinations = new Dictionary<string, DestinationConfig>()
                    {
                        {
                            Guid.NewGuid().ToString("N"),
                            new DestinationConfig()
                            {
                                Address = domainName.Service,
                            }
                        }
                    }
                };

                clusters.Add(cluster);
            }

            if (domainName.ServiceType == ServiceType.ServiceCluster)
            {
                if (string.IsNullOrWhiteSpace(domainName.Path))
                {
                    domainName.Path = "/{**catch-all}";
                }
                else if (domainName.Path == "/")
                {
                    domainName.Path = $"/{{**catch-all}}";
                }
                else
                {
                    domainName.Path = $"/{domainName.Path}/{{**catch-all}}";
                }

                if (domainName.ServiceType == ServiceType.Service)
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
            }

            routes.Add(route);
        }

        return (routes, clusters);
    }
}