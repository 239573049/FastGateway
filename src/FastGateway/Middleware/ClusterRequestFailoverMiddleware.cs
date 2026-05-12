using Core.Entities;
using Core.Entities.Core;
using FastGateway.Services;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Health;
using Yarp.ReverseProxy.Model;

namespace FastGateway.Middleware;

public sealed class ClusterRequestFailoverMiddleware
{
    private static readonly string[] AllowedMethods = [HttpMethods.Get, HttpMethods.Head, HttpMethods.Options];
    private static readonly ConcurrentDictionary<int, HttpMessageInvoker> Clients = new();

    private readonly RequestDelegate _next;
    private readonly string _serverId;
    private readonly string _gatewayVersion;
    private readonly ILogger<ClusterRequestFailoverMiddleware> _logger;

    public ClusterRequestFailoverMiddleware(
        RequestDelegate next,
        string serverId,
        string gatewayVersion,
        ILogger<ClusterRequestFailoverMiddleware> logger)
    {
        _next = next;
        _serverId = serverId;
        _gatewayVersion = gatewayVersion;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ConfigurationService configurationService,
        IProxyStateLookup proxyStateLookup,
        IHttpForwarder httpForwarder,
        IDestinationHealthUpdater destinationHealthUpdater)
    {
        if (!ShouldHandleRequest(context))
        {
            await _next(context);
            return;
        }

        var server = configurationService.GetServer(_serverId);
        if (server is null || !server.EnableRequestFailover)
        {
            await _next(context);
            return;
        }

        var domainName = FindMatchedDomain(configurationService.GetDomainNamesByServerId(_serverId), context);
        if (domainName is null)
        {
            await _next(context);
            return;
        }

        if (!proxyStateLookup.TryGetCluster(domainName.Id, out var cluster) || cluster is null)
        {
            await _next(context);
            return;
        }

        var candidates = GetCandidateDestinations(cluster).ToArray();
        if (candidates.Length <= 1)
        {
            await _next(context);
            return;
        }

        var connectTimeoutMs = server.FailoverConnectTimeoutMs > 0 ? server.FailoverConnectTimeoutMs : 150;
        var budgetMs = server.FailoverBudgetMs >= connectTimeoutMs ? server.FailoverBudgetMs : 500;
        var requestTimeoutSeconds = server.Timeout > 0 ? server.Timeout : 900;
        var reactivationPeriod = GetReactivationPeriod(domainName);
        var attemptedDestinationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stopwatch = Stopwatch.StartNew();
        ForwarderError lastError = ForwarderError.None;
        Exception? lastException = null;

        foreach (var destination in Shuffle(candidates))
        {
            if (!attemptedDestinationIds.Add(destination.DestinationId))
            {
                continue;
            }

            if (stopwatch.ElapsedMilliseconds > budgetMs && attemptedDestinationIds.Count > 1)
            {
                break;
            }

            context.Features.Set<IForwarderErrorFeature?>(null);
            var requestConfig = new ForwarderRequestConfig
            {
                ActivityTimeout = TimeSpan.FromSeconds(requestTimeoutSeconds)
            };
            var transformer = new ClusterFailoverTransformer(domainName, server, _gatewayVersion);
            var httpClient = GetOrCreateClient(connectTimeoutMs);
            var destinationPrefix = destination.Model.Config.Address;

            var error = await httpForwarder.SendAsync(
                context,
                destinationPrefix,
                httpClient,
                requestConfig,
                transformer,
                context.RequestAborted);

            if (error == ForwarderError.None)
            {
                if (attemptedDestinationIds.Count > 1)
                {
                    _logger.LogInformation(
                        "集群请求故障转移成功 ClusterId={ClusterId} DestinationId={DestinationId} Attempts={Attempts} ElapsedMs={ElapsedMs}",
                        cluster.ClusterId,
                        destination.DestinationId,
                        attemptedDestinationIds.Count,
                        stopwatch.ElapsedMilliseconds);
                }
                return;
            }

            var errorFeature = context.Features.Get<IForwarderErrorFeature>();
            lastError = error;
            lastException = errorFeature?.Exception;

            if (IsRetriableTransportError(error, lastException) && !context.Response.HasStarted)
            {
                destinationHealthUpdater.SetPassive(cluster, destination, DestinationHealth.Unhealthy, reactivationPeriod);
                _logger.LogWarning(
                    lastException,
                    "集群请求故障转移，目标切换 ClusterId={ClusterId} DestinationId={DestinationId} Error={Error} Attempt={Attempt} ElapsedMs={ElapsedMs}",
                    cluster.ClusterId,
                    destination.DestinationId,
                    error,
                    attemptedDestinationIds.Count,
                    stopwatch.ElapsedMilliseconds);
                continue;
            }

            return;
        }

        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode = StatusCodes.Status504GatewayTimeout;
            context.Response.Headers["Server"] = "FastGateway";
            context.Response.Headers["X-FastGateway-Version"] = _gatewayVersion;
            await context.Response.WriteAsJsonAsync(new
            {
                Code = StatusCodes.Status504GatewayTimeout,
                Message = "没有可用的健康上游节点或请求级故障转移预算已耗尽",
                Error = lastError.ToString(),
                Detail = lastException?.Message
            });
        }
    }

    private static bool ShouldHandleRequest(HttpContext context)
    {
        if (!AllowedMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        if (context.WebSockets.IsWebSocketRequest)
        {
            return false;
        }

        if (context.Request.Headers.Connection == "Upgrade")
        {
            return false;
        }

        if (context.Request.Headers.Accept.ToString().Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static DomainName? FindMatchedDomain(IEnumerable<DomainName> domainNames, HttpContext context)
    {
        var requestPath = context.Request.Path;
        var requestHost = context.Request.Host.Host;

        return domainNames
            .Where(x => x is { Enable: true, ServiceType: ServiceType.ServiceCluster })
            .Where(x => MatchHost(x, requestHost) && MatchPath(x, requestPath))
            .OrderByDescending(GetHostPriority)
            .ThenByDescending(x => NormalizeRoutePath(x.Path).Length)
            .FirstOrDefault();
    }

    private static int GetHostPriority(DomainName domainName)
    {
        if (domainName.Domains is not { Length: > 0 })
        {
            return 0;
        }

        return domainName.Domains.Any(x => !x.Contains('*')) ? 2 : 1;
    }

    private static bool MatchHost(DomainName domainName, string requestHost)
    {
        if (domainName.Domains is not { Length: > 0 })
        {
            return true;
        }

        foreach (var hostPattern in domainName.Domains)
        {
            if (string.IsNullOrWhiteSpace(hostPattern))
            {
                continue;
            }

            if (hostPattern == "*")
            {
                return true;
            }

            if (string.Equals(hostPattern, requestHost, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (hostPattern.StartsWith("*.", StringComparison.Ordinal) &&
                requestHost.EndsWith(hostPattern[1..], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MatchPath(DomainName domainName, PathString requestPath)
    {
        var routePath = NormalizeRoutePath(domainName.Path);
        if (routePath == "/")
        {
            return true;
        }

        return requestPath.StartsWithSegments(routePath, out _);
    }

    private static string NormalizeRoutePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return "/";
        }

        return "/" + path.Trim().Trim('/');
    }

    private static IEnumerable<DestinationState> GetCandidateDestinations(ClusterState cluster)
    {
        return cluster.Destinations.Values
            .Where(x => GetEffectiveHealth(x.Health) != DestinationHealth.Unhealthy);
    }

    private static DestinationHealth GetEffectiveHealth(DestinationHealthState healthState)
    {
        if (healthState.Active == DestinationHealth.Unhealthy || healthState.Passive == DestinationHealth.Unhealthy)
        {
            return DestinationHealth.Unhealthy;
        }

        if (healthState.Active == DestinationHealth.Unknown || healthState.Passive == DestinationHealth.Unknown)
        {
            return DestinationHealth.Unknown;
        }

        return DestinationHealth.Healthy;
    }

    private static IEnumerable<DestinationState> Shuffle(IReadOnlyCollection<DestinationState> destinations)
    {
        return destinations.OrderBy(_ => Random.Shared.Next());
    }

    private static TimeSpan GetReactivationPeriod(DomainName domainName)
    {
        var seconds = domainName.HealthCheckIntervalSeconds;
        if (seconds <= 0)
        {
            seconds = 10;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static bool IsRetriableTransportError(ForwarderError error, Exception? exception)
    {
        if (exception is HttpRequestException or SocketException or TaskCanceledException or TimeoutException)
        {
            return true;
        }

        return error is ForwarderError.Request
            or ForwarderError.RequestTimedOut
            or ForwarderError.ResponseHeaders
            or ForwarderError.RequestCreation;
    }

    private static HttpMessageInvoker GetOrCreateClient(int connectTimeoutMs)
    {
        return Clients.GetOrAdd(connectTimeoutMs, static timeout =>
        {
            var handler = new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                EnableMultipleHttp2Connections = true,
                ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current),
                RequestHeaderEncodingSelector = (_, _) => Encoding.UTF8,
                ConnectTimeout = TimeSpan.FromMilliseconds(timeout),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(1),
                ResponseDrainTimeout = TimeSpan.FromSeconds(30)
            };

            return new HttpMessageInvoker(handler, disposeHandler: true);
        });
    }

    private sealed class ClusterFailoverTransformer(DomainName domainName, Server server, string gatewayVersion)
        : HttpTransformer
    {
        public override async ValueTask TransformRequestAsync(
            HttpContext httpContext,
            HttpRequestMessage proxyRequest,
            string destinationPrefix,
            CancellationToken cancellationToken)
        {
            await base.TransformRequestAsync(httpContext, proxyRequest, destinationPrefix, cancellationToken);

            var routePath = NormalizeRoutePath(domainName.Path);
            var forwardPath = httpContext.Request.Path;
            if (routePath != "/" && httpContext.Request.Path.StartsWithSegments(routePath, out var remaining))
            {
                forwardPath = remaining.HasValue ? remaining : new PathString("/");
            }

            proxyRequest.RequestUri = RequestUtilities.MakeDestinationAddress(destinationPrefix, forwardPath, httpContext.Request.QueryString);

            if (domainName.Domains.Any(x => x.Contains('*')) || server.CopyRequestHost)
            {
                proxyRequest.Headers.Host = httpContext.Request.Host.Value;
            }
        }

        public override async ValueTask<bool> TransformResponseAsync(
            HttpContext httpContext,
            HttpResponseMessage? proxyResponse,
            CancellationToken cancellationToken)
        {
            var shouldCopy = await base.TransformResponseAsync(httpContext, proxyResponse, cancellationToken);
            if (!shouldCopy)
            {
                return false;
            }

            httpContext.Response.Headers.Remove("Server");
            httpContext.Response.Headers["Server"] = "FastGateway";
            httpContext.Response.Headers["X-FastGateway-Version"] = gatewayVersion;
            return true;
        }
    }
}

public static class ClusterRequestFailoverMiddlewareExtensions
{
    public static IApplicationBuilder UseClusterRequestFailover(this IApplicationBuilder app, string serverId, string gatewayVersion)
    {
        return app.UseMiddleware<ClusterRequestFailoverMiddleware>(serverId, gatewayVersion);
    }
}
