using System.Diagnostics;
using System.Net;
using Yarp.ReverseProxy.Forwarder;

namespace FastGateway.Gateway
{
    public class FastGatewayForwarderHttpClientFactory : IForwarderHttpClientFactory
    {
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
        {
            var handler = new SocketsHttpHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None | DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                UseCookies = false,
                EnableMultipleHttp2Connections = true,
                ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current),
                ConnectTimeout = TimeSpan.FromSeconds(600),
            };

            return new HttpMessageInvoker(handler, disposeHandler: true);
        }
    }
}
