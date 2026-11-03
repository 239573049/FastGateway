using System.Net;
using System.Net.NetworkInformation;

namespace FastGateway.Security;

public readonly record struct HttpPathSignature(int Weight, string Reason);

/// <summary>
///     Jev 威胁研判使用的请求特征：过滤内网/本机 IP，并给已知扫描路径打权重。
///     仅用于离线研判的特征提取，不在请求热路径上做任何拦截。
/// </summary>
public static class HttpThreatSignals
{
    private static readonly HashSet<string> LocalAddresses = LoadLocalAddresses();

    private static readonly HashSet<string> RiskyMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "TRACE", "CONNECT", "PROPFIND", "PROPPATCH", "MKCOL", "COPY", "MOVE", "LOCK", "UNLOCK", "DEBUG"
    };

    private static readonly HashSet<string> KnownMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "GET", "HEAD", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "TRACE", "CONNECT",
        "PROPFIND", "PROPPATCH", "MKCOL", "COPY", "MOVE", "LOCK", "UNLOCK", "DEBUG"
    };

    public static bool ShouldTrackIp(string? rawIp)
    {
        if (!IPAddress.TryParse(rawIp, out var address)) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal)
            return false;
        if (LocalAddresses.Contains(address.ToString())) return false;

        if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return !(bytes[0] == 0 ||
                     bytes[0] == 10 ||
                     bytes[0] == 127 ||
                     bytes[0] >= 224 ||
                     bytes[0] == 169 && bytes[1] == 254 ||
                     bytes[0] == 172 && bytes[1] is >= 16 and <= 31 ||
                     bytes[0] == 192 && bytes[1] == 168 ||
                     bytes[0] == 100 && bytes[1] is >= 64 and <= 127);
        }

        return !(address.IsIPv6Multicast || address.GetAddressBytes()[0] is 0xfc or 0xfd);
    }

    private static HashSet<string> LoadLocalAddresses()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                foreach (var unicast in networkInterface.GetIPProperties().UnicastAddresses)
                {
                    var address = unicast.Address;
                    if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
                    result.Add(address.ToString());
                }
            }
        }
        catch
        {
            // 读取网卡失败时仍保留回环/私网过滤，不能让检测器影响请求处理。
        }

        return result;
    }

    public static HttpPathSignature AnalyzePath(string path, string method)
    {
        var normalized = NormalizePath(path);
        return Classify(normalized, method);
    }

    private static HttpPathSignature Classify(string path, string method)
    {
        var weights = new List<(int Weight, string Reason)>(4);

        if (Contains(path, "/.env"))
            weights.Add((75, "探测 .env 环境变量"));
        if (ContainsAny(path, "/.git", "/.svn", "/.hg", "/.ds_store"))
            weights.Add((75, "探测版本控制或系统元数据"));
        if (ContainsAny(path, "/etc/passwd", "/proc/self", "/id_rsa", "/.ssh/", "/private_key"))
            weights.Add((90, "敏感文件或路径穿越"));
        if (ContainsAny(path, "../", "..\\", "%2e%2e", "%252e%252e"))
            weights.Add((90, "路径穿越"));
        if (ContainsAny(path, "appsettings.json", "gateway.config", "web.config", "credentials", "secret"))
            weights.Add((85, "探测配置文件或凭据"));
        if (ContainsAny(path, "169.254.169.254", "/latest/meta-data", "/computemetadata"))
            weights.Add((90, "探测云元数据"));
        if (ContainsAny(path, "jndi:", "${", "%24%7b"))
            weights.Add((90, "注入载荷特征"));
        if (path.Contains("/sdk/weblanguage", StringComparison.Ordinal))
            weights.Add((85, "设备命令注入探测"));
        if (ContainsAny(path, "/wp-login", "/wp-admin", "/xmlrpc.php", "/phpmyadmin", "/adminer",
                "/admin/config", "/manager/html", "/actuator", "/console", "/jenkins", "/v2/_catalog",
                "/server-status", "/cgi-bin"))
            weights.Add((60, "管理面或组件漏洞扫描"));
        if (HasScriptExtension(path))
            weights.Add((50, "脚本文件扫描"));
        if (ContainsAny(path, "/mcp", "/sse", "/api/mcp"))
            weights.Add((60, "探测暴露的 AI/SSE 服务"));

        if (RiskyMethods.Contains(method))
            weights.Add((50, $"异常 HTTP 方法 {method}"));
        else if (!KnownMethods.Contains(method))
            weights.Add((30, $"非标准 HTTP 方法 {method}"));


        var weight = Math.Min(90, weights.Sum(x => x.Weight));
        return new HttpPathSignature(weight,
            string.Join("；", weights.Select(x => x.Reason).Distinct(StringComparer.Ordinal)));
    }

    private static string NormalizePath(string path)
    {
        var current = path;
        for (var i = 0; i < 2; i++)
        {
            try
            {
                var decoded = Uri.UnescapeDataString(current);
                if (decoded == current) break;
                current = decoded;
            }
            catch (UriFormatException)
            {
                break;
            }
        }

        current = current.Replace('\\', '/').ToLowerInvariant();
        while (current.Contains("//", StringComparison.Ordinal))
            current = current.Replace("//", "/", StringComparison.Ordinal);
        return current.Length > 2048 ? current[..2048] : current;
    }

    private static bool Contains(string path, string value) => path.Contains(value, StringComparison.Ordinal);

    private static bool ContainsAny(string path, params string[] values)
    {
        foreach (var value in values)
        {
            if (path.Contains(value, StringComparison.Ordinal)) return true;
        }

        return false;
    }

    private static bool HasScriptExtension(string path)
    {
        return path.EndsWith(".php", StringComparison.Ordinal) ||
               path.EndsWith(".jsp", StringComparison.Ordinal) ||
               path.EndsWith(".aspx", StringComparison.Ordinal) ||
               path.EndsWith(".asp", StringComparison.Ordinal) ||
               path.EndsWith(".cgi", StringComparison.Ordinal);
    }
}
