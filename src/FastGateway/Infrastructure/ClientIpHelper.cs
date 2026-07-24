using System.Collections.Concurrent;
using System.Net;
using Core.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace FastGateway.Infrastructure;

public static class ClientIpHelper
{
    private static readonly ConcurrentDictionary<string, ClientIpSource> ServerSources = new();
    private static readonly object ResolvedClientIpKey = new();

    public const string ResolvedClientIpHeader = "X-FastGateway-Client-IP";

    public static void SetClientIpSource(string serverId, ClientIpSource source)
    {
        if (string.IsNullOrWhiteSpace(serverId)) return;
        ServerSources[serverId] = source;
    }

    public static void RemoveClientIpSource(string serverId)
    {
        if (string.IsNullOrWhiteSpace(serverId)) return;
        ServerSources.TryRemove(serverId, out _);
    }

    public static IApplicationBuilder UseClientIpResolution(this IApplicationBuilder app, string serverId,
        ClientIpSource defaultSource)
    {
        SetClientIpSource(serverId, defaultSource);

        return app.Use(async (context, next) =>
        {
            context.Request.Headers.Remove(ResolvedClientIpHeader);

            var source = ServerSources.TryGetValue(serverId, out var configuredSource)
                ? configuredSource
                : defaultSource;
            context.Items[ResolvedClientIpKey] = GetClientIp(context, source);
            await next(context);
        });
    }

    public static string GetClientIp(HttpContext context)
    {
        if (context.Items.TryGetValue(ResolvedClientIpKey, out var value) && value is string ip)
            return ip;

        return GetClientIp(context, ClientIpSource.Default);
    }

    public static string GetClientIp(HttpContext context, ClientIpSource source)
    {
        return source switch
        {
            ClientIpSource.XForwardedFor => GetHeaderIpOrRemote(context, "X-Forwarded-For"),
            ClientIpSource.XRealIp => GetHeaderIpOrRemote(context, "X-Real-IP"),
            ClientIpSource.CfConnectingIp => GetHeaderIpOrRemote(context, "CF-Connecting-IP"),
            _ => GetRemoteIp(context)
        };
    }

    private static string GetHeaderIpOrRemote(HttpContext context, string headerName)
    {
        return TryGetHeaderIp(context, headerName, out var ip) ? ip : GetRemoteIp(context);
    }

    private static bool TryGetHeaderIp(HttpContext context, string headerName, out string ip)
    {
        ip = string.Empty;
        if (!context.Request.Headers.TryGetValue(headerName, out var headerValue)) return false;

        var raw = headerValue.ToString();
        if (string.IsNullOrWhiteSpace(raw)) return false;

        return TryParseIp(raw.Split(',')[0].Trim(), out ip);
    }

    private static string GetRemoteIp(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;
        return remote == null ? string.Empty : Normalize(remote);
    }

    private static bool TryParseIp(string raw, out string ip)
    {
        ip = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (IPAddress.TryParse(raw, out var address))
        {
            ip = Normalize(address);
            return true;
        }

        if (IPEndPoint.TryParse(raw, out var endpoint))
        {
            ip = Normalize(endpoint.Address);
            return true;
        }

        return false;
    }

    /// <summary>
    ///     双栈监听下 IPv4 客户端表现为 IPv4 映射地址（::ffff:1.2.3.4），
    ///     必须还原为纯 IPv4，否则黑白名单/限流/归属地全部无法匹配。
    /// </summary>
    private static string Normalize(IPAddress address)
    {
        return (address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address).ToString();
    }
}

