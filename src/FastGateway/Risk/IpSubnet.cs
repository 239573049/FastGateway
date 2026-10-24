using System.Net;
using System.Net.Sockets;

namespace FastGateway.Risk;

public static class IpSubnet
{
    /// <summary>
    ///     IPv4 归入 /24，IPv6 归入 /64（通常对应同一用户或同一机房分配的地址段）
    /// </summary>
    public static string? GetKey(string? ip)
    {
        if (string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out var address)) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        var bytes = address.GetAddressBytes();
        switch (address.AddressFamily)
        {
            case AddressFamily.InterNetwork:
                bytes[3] = 0;
                return $"{new IPAddress(bytes)}/24";
            case AddressFamily.InterNetworkV6:
                Array.Clear(bytes, 8, 8);
                return $"{new IPAddress(bytes)}/64";
            default:
                return null;
        }
    }
}
