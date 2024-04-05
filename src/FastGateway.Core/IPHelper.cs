using System.Net;
using System.Runtime.Serialization.DataContracts;

namespace FastGateway.Core;

public class IpHelper
{
    /// <summary>
    /// 校验ip是否在范围内
    /// IP格则，10.0.0.1-10.0.0.255 或 172.16.0.1/24 或 192.168.1.1 ,这个时候需要判断ip是否在白名单的范围内，
    /// 如果在范围内则返回true
    /// </summary>
    public static unsafe bool UnsafeCheckIpInIpRange(string ip, string ipRange)
    {
        // 还有可能是单个ip
        if (ip == ipRange)
        {
            return true;
        }

        if (ipRange.Contains('-'))
        {
            var ipRanges = ipRange.Split('-');
            var startIp = ipRanges[0];
            var endIp = ipRanges[1];

            fixed (char* startIpPtr = startIp)
            fixed (char* endIpPtr = endIp)
            fixed (char* ipPtr = ip)
            {
                for (var i = 0; i < 15; i++)
                {
                    if (startIpPtr[i] == endIpPtr[i])
                    {
                        if (startIpPtr[i] != ipPtr[i])
                        {
                            return false;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return true;
        }

        if (!ipRange.Contains('/')) return false;
        {
            var ipRanges = ipRange.Split('/');
            var startIp = ipRanges[0];
            var mask = int.Parse(ipRanges[1]);

            fixed (char* startIpPtr = startIp)
            fixed (char* ipPtr = ip)
            {
                for (var i = 0; i < 15; i++)
                {
                    if (mask == 0)
                    {
                        break;
                    }

                    if (startIpPtr[i] != ipPtr[i])
                    {
                        return false;
                    }

                    if (startIpPtr[i] == '.')
                    {
                        mask--;
                    }
                }
            }

            return true;
        }
    }

    /// <summary>
    /// 校验ip是否在范围内
    /// IP格则，10.0.0.1-10.0.0.255 或 172.16.0.1/24 或 192.168.1.1 ,这个时候需要判断ip是否在白名单的范围内，
    /// 如果在范围内则返回true
    /// </summary>
    public static bool CheckIpInIpRange(string ip, string ipRange)
    {
        // 还有可能是单个ip
        if (ip == ipRange)
        {
            return true;
        }

        if (ipRange.Contains('-'))
        {
            var ipRanges = ipRange.Split('-');
            var startIp = ipRanges[0];
            var endIp = ipRanges[1];

            var startIpArray = startIp.Split('.');
            var endIpArray = endIp.Split('.');

            var ipArray = ip.Split('.');

            for (var i = 0; i < 4; i++)
            {
                if (int.Parse(ipArray[i]) < int.Parse(startIpArray[i]) ||
                    int.Parse(ipArray[i]) > int.Parse(endIpArray[i]))
                {
                    return false;
                }
            }

            return true;
        }

        if (ipRange.Contains('/'))
        {
            var ipRanges = ipRange.Split('/');
            var startIp = ipRanges[0];
            var mask = int.Parse(ipRanges[1]);

            var startIpArray = startIp.Split('.');
            var ipArray = ip.Split('.');

            for (var i = 0; i < 4; i++)
            {
                var startIpInt = int.Parse(startIpArray[i]);
                var ipInt = int.Parse(ipArray[i]);

                var startIpBinary = Convert.ToString(startIpInt, 2).PadLeft(8, '0');
                var ipBinary = Convert.ToString(ipInt, 2).PadLeft(8, '0');

                for (var j = 0; j < mask; j++)
                {
                    if (startIpBinary[j] != ipBinary[j])
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        return false;
    }

}
