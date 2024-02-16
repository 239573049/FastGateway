using FastGateway.Contract;

namespace Microsoft.Extensions.DependencyInjection;

public static class HomeAddressExtension
{
    /// <summary>
    /// 添加在线获取IP归属地
    /// </summary>
    public static void AddOnLineHomeAddress(this IServiceCollection services)
    {
        services.AddHttpClient(nameof(OnLineHomeAddressService), options =>
        {
            options.Timeout = TimeSpan.FromSeconds(10);
            options.DefaultRequestHeaders.Add("Accept", "application/json");
            options.DefaultRequestHeaders.Add("User-Agent", "FastGateway-OnLine");
            
        });
        services.AddSingleton<IHomeAddressService,OnLineHomeAddressService>();
    }
    
    /// <summary>
    /// 添加离线获取IP归属地
    /// </summary>
    public static void AddOfflineHomeAddress(this IServiceCollection services)
    {
        services.AddSingleton<IHomeAddressService,OfflineLibraryHomeAddressService>();
    }
}