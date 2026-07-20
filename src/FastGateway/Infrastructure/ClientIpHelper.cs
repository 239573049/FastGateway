using System.Net;
using Microsoft.AspNetCore.Http;

namespace FastGateway.Infrastructure;

public static class ClientIpHelper
{
    public static string GetClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
        {
            var raw = forwardedFor.ToString();
            if (!string.IsNullOrWhiteSpace(raw))
            {
                var candidate = raw.Split(',')[0].Trim();
                if (TryParseIp(candidate, out var ip))
                {
                    return ip;
                }
            }
        }

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

