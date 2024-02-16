using FastGateway.Contract;
using IP2Region.Net.Abstractions;
using IP2Region.Net.XDB;

namespace FastGateway.Services;

/// <summary>
/// 获取离线IP归属地
/// </summary>
public sealed class OfflineLibraryHomeAddressService : IHomeAddressService
{
    private static ISearcher _searcher;

    public OfflineLibraryHomeAddressService()
    {
        _searcher = new Searcher(CachePolicy.File, "./ip2region.xdb");
    }

    public async Task<string?> GetHomeAddress(string ip)
    {
        return await Task.Run((() => _searcher.Search(ip)?.Split("|").LastOrDefault()?.Trim() ?? "未知地址"))
            .ConfigureAwait(false);
    }

    public async IAsyncEnumerable<string?> GetHomeAddress(IEnumerable<string> ips)
    {
        foreach (var ip in ips)
        {
            yield return await GetHomeAddress(ip);
        }
    }
}