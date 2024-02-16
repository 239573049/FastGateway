namespace FastGateway.Contract;

public interface IHomeAddressService
{
    /// <summary>
    /// 获取IP归属地
    /// </summary>
    /// <param name="ip"></param>
    /// <returns></returns>
    Task<string?> GetHomeAddress(string ip);
    
    /// <summary>
    /// 批量获取IP归属地
    /// </summary>
    /// <param name="ips"></param>
    /// <returns></returns>
    IAsyncEnumerable<string?> GetHomeAddress(IEnumerable<string> ips);
}