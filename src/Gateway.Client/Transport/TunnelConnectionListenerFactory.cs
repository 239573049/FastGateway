using System.Net;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Options;

namespace Gateway.Client.Transport;

public class TunnelConnectionListenerFactory(IOptions<TunnelOptions> options) : IConnectionListenerFactory
{
    private readonly TunnelOptions _options = options.Value;

    public ValueTask<IConnectionListener> BindAsync(EndPoint endpoint, CancellationToken cancellationToken = default)
    {
        return new(new TunnelConnectionListener(_options, endpoint));
    }
}