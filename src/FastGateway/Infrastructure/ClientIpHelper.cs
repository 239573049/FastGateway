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

        var remote = context.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(remote) ? string.Empty : remote;
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
            ip = address.ToString();
            return true;
        }

        if (IPEndPoint.TryParse(raw, out var endpoint))
        {
            ip = endpoint.Address.ToString();
            return true;
        }

        return false;
    }
}

