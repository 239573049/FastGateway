using System.Net;
using FastGateway.Contract;

namespace FastGateway.Services;

/// <summary>
/// 在线获取IP归属地
/// </summary>
public sealed class OnLineHomeAddressService(IHttpClientFactory httpClientFactory) : IHomeAddressService
{
    public async Task<string?> GetHomeAddress(string ip)
    {
        var client = httpClientFactory.CreateClient(nameof(OnLineHomeAddressService));

        var response = await client.GetAsync($"https://whois.pconline.com.cn/ipJson.jsp?ip={ip}&json=true");

        if (response.StatusCode != HttpStatusCode.OK)
            return default;

        var result = await response.Content.ReadFromJsonAsync<HomeAddressResultDto>();

        return result?.city;
    }

    public async IAsyncEnumerable<string?> GetHomeAddress(IEnumerable<string> ips)
    {
        foreach (var ip in ips)
        {
            yield return await GetHomeAddress(ip);
        }
    }

    private sealed record HomeAddressResultDto(
        string ip,
        string pro,
        string proCode,
        string city,
        string cityCode,
        string region,
        string regionCode,
        string addr,
        string regionNames,
        string err);
}