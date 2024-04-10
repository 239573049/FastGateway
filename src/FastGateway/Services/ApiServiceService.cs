using System.Net;
using System.Security.Cryptography.X509Certificates;
using FastGateway.Infrastructures;
using FastGateway.Middlewares;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms;
using Directory = System.IO.Directory;

namespace FastGateway.Services;

public static class ApiServiceService
{
    private static readonly Dictionary<string, WebApplication> WebApplications = new();

    public static async Task LoadServices(IFreeSql freeSql)
    {
        foreach (var application in WebApplications)
        {
            await application.Value.DisposeAsync();
        }

        WebApplications.Clear();

        var services = await freeSql.Select<Service>()
            .IncludeMany(x => x.Locations)
            .ToListAsync();

        foreach (var service in services)
        {
            await Task.Factory.StartNew(BuilderService, service);
        }
    }

    [Authorize]
    public static async Task CreateAsync(ServiceInput input, IFreeSql freeSql)
    {
        if (await freeSql.Select<Service>().Where(x => x.Listen == input.Listen).CountAsync() > 0)
        {
            throw new Exception("端口已被占用");
        }

        // 可能是更新
        if (!string.IsNullOrEmpty(input.Id) && await freeSql.Select<Service>().AnyAsync(x => x.Id == input.Id))
        {
            await UpdateAsync(input.Id, input, freeSql);
            return;
        }

        var serviceId = Guid.NewGuid().ToString("N");
        var service = new Service()
        {
            Id = serviceId,
            Enable = input.Enable,
            EnableFlowMonitoring = input.EnableFlowMonitoring,
            EnableTunnel = input.EnableTunnel,
            EnableWhitelist = input.EnableWhitelist,
            EnableBlacklist = input.EnableBlacklist,
            IsHttps = input.IsHttps,
            Listen = input.Listen,
            Locations = input.Locations.Select(x => new Location()
            {
                ServiceId = serviceId,
                Id = Guid.NewGuid().ToString("N"),
                ServiceNames = x.ServiceNames,
                LocationService = x.LocationService.Select(x => new LocationService()
                {
                    AddHeader = x.AddHeader,
                    Path = x.Path,
                    ProxyPass = x.ProxyPass,
                    Root = x.Root,
                    Type = x.Type,
                    TryFiles = x.TryFiles,
                    LoadType = x.LoadType,
                    UpStreams = x.UpStreams.Select(x => new UpStream()
                    {
                        Server = x.Server,
                        Weight = x.Weight
                    }).ToList()
                }).ToList()
            }).ToList()
        };

        await freeSql.Insert(service).ExecuteAffrowsAsync();

        await freeSql.Insert(service.Locations).ExecuteAffrowsAsync();

        await Task.Factory.StartNew(BuilderService, service);
    }

    [Authorize]
    public static async Task UpdateAsync(string id, ServiceInput input, IFreeSql freeSql)
    {
        await freeSql.Update<Service>()
            .Where(x => x.Id == id)
            .Set(x => x.Listen, input.Listen)
            .Set(x => x.IsHttps, input.IsHttps)
            .Set(x => x.EnableBlacklist, input.EnableBlacklist)
            .Set(x => x.EnableWhitelist, input.EnableWhitelist)
            .Set(x => x.Enable, input.Enable)
            .Set(x => x.EnableTunnel, input.EnableTunnel)
            .Set(x => x.EnableFlowMonitoring, input.EnableFlowMonitoring)
            .ExecuteAffrowsAsync();

        await freeSql.Delete<Location>()
            .Where(x => x.ServiceId == id)
            .ExecuteAffrowsAsync();

        await freeSql.Insert(input.Locations.Select(x => new Location()
        {
            ServiceNames = x.ServiceNames.Where(x => !string.IsNullOrEmpty(x)).ToArray(),
            Id = Guid.NewGuid().ToString("N"),
            ServiceId = id,
            LocationService = x.LocationService.Select(x => new LocationService()
            {
                AddHeader = x.AddHeader,
                Path = x.Path,
                ProxyPass = x.ProxyPass,
                Root = x.Root,
                Type = x.Type,
                TryFiles = x.TryFiles,
                UpStreams = x.UpStreams.Select(x => new UpStream()
                {
                    Server = x.Server,
                    Weight = x.Weight
                }).ToList(),
                LoadType = x.LoadType,
            }).ToList()
        })).ExecuteAffrowsAsync();
    }

    [Authorize]
    public static async Task DeleteAsync(string id, IFreeSql freeSql)
    {
        if (WebApplications.Remove(id, out var app))
        {
            await app.DisposeAsync();
        }

        await freeSql.Delete<Service>()
            .Where(x => x.Id == id)
            .ExecuteAffrowsAsync();

        await freeSql.Delete<Location>()
            .Where(x => x.ServiceId == id)
            .ExecuteAffrowsAsync();
    }

    /// <summary>
    /// 校验目录是否存在
    /// </summary>
    /// <param name="path"></param>
    /// <returns></returns>
    public static ResultDto<bool> CheckDirectoryExistenceAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ResultDto<bool>.ErrorResult("Path is empty");
        }

        // 判断path是文件还是目录
        var isDirectory = path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar);

        if (isDirectory)
        {
            return !Directory.Exists(Path.GetDirectoryName(path))
                ? ResultDto<bool>.ErrorResult($"未找到：{path}")
                : ResultDto<bool>.SuccessResult(true);
        }
        else
        {
            return !Directory.Exists(path)
                ? ResultDto<bool>.ErrorResult($"未找到：{path}")
                : ResultDto<bool>.SuccessResult(true);
        }
    }

    [Authorize]
    public static async Task<Service?> GetAsync(string id, IFreeSql freeSql)
    {
        return await freeSql.Select<Service>()
            .IncludeMany(x => x.Locations)
            .Where(x => x.Id == id)
            .FirstAsync();
    }

    [Authorize]
    public static async Task<ResultDto<PageResultDto<Service>>> GetListAsync(int page, int pageSize,
        IFreeSql freeSql)
    {
        if (page < 1)
        {
            page = 1;
        }

        pageSize = pageSize switch
        {
            < 1 => 10,
            > 100 => 100,
            _ => pageSize
        };

        var result = await freeSql.Select<Service>()
            .IncludeMany(x => x.Locations)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var total = await freeSql.Select<Service>().CountAsync();

        return ResultDto<PageResultDto<Service>>.SuccessResult(new PageResultDto<Service>(result, total));
    }

    [Authorize]
    public static ResultDto<Dictionary<string, bool>> ServiceStats([FromBody] List<string> ids)
    {
        var result = new Dictionary<string, bool>();

        foreach (var id in ids)
        {
            if (WebApplications.TryGetValue(id, out var app))
            {
                result.Add(id, true);
            }
        }

        return ResultDto<Dictionary<string, bool>>.SuccessResult(result);
    }

    [Authorize]
    public static async Task StartServiceAsync(string id, IFreeSql freeSql)
    {
        if (WebApplications.TryGetValue(id, out var app))
        {
            await app.StartAsync();
        }
        else
        {
            var service = await GetAsync(id, freeSql);

            await Task.Factory.StartNew(BuilderService, service);

            await Task.Delay(500);
        }
    }

    [Authorize]
    public static async Task StopServiceAsync(string id)
    {
        if (WebApplications.TryGetValue(id, out var app))
        {
            await app.StopAsync();
            WebApplications.Remove(id);
        }
    }

    [Authorize]
    public static async Task RestartServiceAsync(string id, IFreeSql freeSql)
    {
        if (WebApplications.TryGetValue(id, out var app))
        {
            await app.StopAsync();

            await app.DisposeAsync();

            WebApplications.Remove(id, out _);

            var service = await GetAsync(id, freeSql);

            await Task.Factory.StartNew(BuilderService, service);

            await Task.Delay(500);
        }
    }

    [Authorize]
    public static async Task RestartConfigAsync(string id, IFreeSql freeSql)
    {
        var service = await GetAsync(id, freeSql);

        if (service == null)
        {
            return;
        }

        if (WebApplications.TryGetValue(id, out var app))
        {
            var (routes, clusters) = BuilderGateway(service);

            var memoryConfigProvider = app.Services.GetRequiredService<InMemoryConfigProvider>();

            memoryConfigProvider.Update(routes, clusters);
        }
    }

    private static async Task BuilderService(object state)
    {
        try
        {
            var service = (Service)state;

            var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());

            var defaultContentTypeProvider = new DefaultContentTypeProvider();

            builder.WebHost.UseKestrel(options =>
            {
                options.ConfigureHttpsDefaults(adapterOptions =>
                {
                    if (service.IsHttps)
                    {
                        adapterOptions.ServerCertificateSelector = (context, name) =>
                            CertService.Certs.TryGetValue(name, out var cert)
                                ? new X509Certificate2(cert.File, cert.Password)
                                : new X509Certificate2(Path.Combine(AppContext.BaseDirectory, "gateway.pfx"), "010426");
                    }
                });

                options.Listen(IPAddress.Parse("0.0.0.0"), service.Listen, listenOptions =>
                {
                    if (!service.IsHttps) return;

                    listenOptions.UseHttps();

                    listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
                });
            });

            var (routes, clusters) = BuilderGateway(service);

            builder.Services.AddSingleton<ICurrentContext>(new CurrentContext()
            {
                ServiceId = service.Id,
            }).AddSingleton<StatisticsMiddleware>();

            builder.Services.AddReverseProxy()
                .LoadFromMemory(routes, clusters)
                // 删除所有代理的前缀
                .AddTransforms(context =>
                {
                    var prefix = context.Route.Match.Path?.Replace("/{**catch-all}", "");
                    if (!string.IsNullOrEmpty(prefix))
                    {
                        context.AddPathRemovePrefix(prefix);
                    }
                });

            var app = builder.Build();

            WebApplications.Add(service.Id, app);

            // 如果启用白名单则添加中间件
            if (service.EnableWhitelist)
            {
                app.Use(async (context, next) =>
                {
                    // 获取当前请求的IP
                    var ip = context.Connection.RemoteIpAddress?.ToString();
                    if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
                    {
                        ip = forwardedFor;
                    }

                    if (ProtectionService.CheckBlacklistAndWhitelist(ip, ProtectionType.Whitelist))
                    {
                        context.Response.StatusCode = 403;
                        return;
                    }

                    await next(context);
                });
            }
            else if (service.EnableBlacklist)
            {
                app.Use(async (context, next) =>
                {
                    // 获取当前请求的IP
                    var ip = context.Connection.RemoteIpAddress?.ToString();
                    if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
                    {
                        ip = forwardedFor;
                    }

                    if (ProtectionService.CheckBlacklistAndWhitelist(ip, ProtectionType.Blacklist))
                    {
                        context.Response.StatusCode = 403;
                        return;
                    }

                    await next(context);
                });
            }

            app.UseMiddleware<StatisticsMiddleware>();

            // 用于HTTPS证书签名校验
            app.MapGet("/.well-known/acme-challenge/{token}", AcmeChallenge.Challenge);

            foreach (var location in service.Locations.SelectMany(x => x.LocationService)
                         .Where(x => x.Type == ApiServiceType.StaticProxy))
            {
                app.Map(location.Path.TrimEnd('/'), app =>
                {
                    app.Run((async context =>
                    {
                        var path = Path.Combine(location.Root, context.Request.Path.Value[1..]);

                        if (File.Exists(path))
                        {
                            defaultContentTypeProvider.TryGetContentType(path, out var contentType);
                            context.Response.Headers.ContentType = contentType;

                            await context.Response.SendFileAsync(path);

                            return;
                        }

                        if (location.TryFiles == null || location.TryFiles.Length == 0)
                        {
                            context.Response.StatusCode = 404;
                            return;
                        }

                        // 搜索 try_files
                        foreach (var tryFile in location.TryFiles)
                        {
                            var tryPath = Path.Combine(location.Root, tryFile);

                            if (!File.Exists(tryPath)) continue;

                            defaultContentTypeProvider.TryGetContentType(tryPath, out var contentType);
                            context.Response.Headers.ContentType = contentType;

                            await context.Response.SendFileAsync(tryPath);

                            return;
                        }
                    }));
                });
            }

            app.MapReverseProxy();

            await app.RunAsync();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    private static (List<RouteConfig>, List<ClusterConfig>) BuilderGateway(Service service)
    {
        var routes = new List<RouteConfig>();
        var clusters = new List<ClusterConfig>();
        // 绑定路由到service
        foreach (var location in service.Locations)
        {
            var clusterId = location.Id;
            var destinations = new Dictionary<string, DestinationConfig>();

            var cluster = new ClusterConfig()
            {
                Destinations = destinations,
                ClusterId = clusterId,
            };
            clusters.Add(cluster);
            foreach (var locationService in location.LocationService)
            {
                var route = new RouteConfig()
                {
                    RouteId = Guid.NewGuid().ToString("N"),
                    ClusterId = clusterId,
                    Match = new RouteMatch()
                    {
                        Path = locationService.Path.TrimEnd('/') + "/{**catch-all}",
                        Hosts = location.ServiceNames
                    }
                };
                routes.Add(route);


                if (locationService.Type == ApiServiceType.SingleService)
                {
                    destinations.Add(Guid.NewGuid().ToString("N"), new DestinationConfig()
                    {
                        Address = locationService.ProxyPass,
                        Host = new Uri(locationService.ProxyPass).Host,
                    });
                    continue;
                }

                if (locationService.Type == ApiServiceType.LoadBalance)
                {
                    foreach (var upStream in locationService.UpStreams.Where(x => !string.IsNullOrEmpty(x.Server)))
                    {
                        destinations.Add(upStream.Server!, new DestinationConfig()
                        {
                            Address = upStream.Server!,
                        });
                    }
                }
                else if (locationService.Type == ApiServiceType.StaticProxy)
                {
                    routes.Remove(route);
                    clusters.Remove(cluster);
                }
            }
        }

        return (routes, clusters);
    }
}