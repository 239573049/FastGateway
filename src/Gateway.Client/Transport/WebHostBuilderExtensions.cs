using Gateway.Client.Options;
using Microsoft.AspNetCore.Connections;

namespace Gateway.Client.Transport;

public static class WebHostBuilderExtensions
{
    public static IWebHostBuilder UseTunnelTransport(this IWebHostBuilder hostBuilder, Action<TunnelOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(TunnelClientOptions.Url);

        hostBuilder.ConfigureKestrel(options =>
        {
            options.Listen(new UriEndPoint2(new Uri(TunnelClientOptions.Url)));
        });

        return hostBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<IConnectionListenerFactory, TunnelConnectionListenerFactory>();

            if (configure is not null)
            {
                services.Configure(configure);
            }
        });
    }
}