using FastGateway.Contract;

namespace FastGateway.Services;

/// <summary>
/// 获取离线IP归属地
/// </summary>
public class OfflineLibraryHomeAddressService : IHomeAddressService
{
    public Task<string> GetHomeAddress(string ip)
    {
        throw new NotImplementedException();
    }

    public IAsyncEnumerable<string?> GetHomeAddress(IEnumerable<string> ips)
    {
        throw new NotImplementedException();
    }
}