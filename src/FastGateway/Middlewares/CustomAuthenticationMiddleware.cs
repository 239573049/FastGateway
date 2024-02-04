using Microsoft.AspNetCore.Authentication;
using Yarp.ReverseProxy.Model;

namespace FastGateway.Middlewares;

/// <summary>
///     自定义授权中间件
/// </summary>
public class CustomAuthenticationMiddleware
{
    /// <summary>
    ///     请求委托
    /// </summary>
    private readonly RequestDelegate _next;

    /// <summary>
    ///     Initializes a new instance of <see cref="AuthenticationMiddleware" />.
    /// </summary>
    /// <param name="next">The next item in the middleware pipeline.</param>
    /// <param name="schemes">The <see cref="IAuthenticationSchemeProvider" />.</param>
    public CustomAuthenticationMiddleware(RequestDelegate next, IAuthenticationSchemeProvider schemes)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(schemes);

        _next = next;
        Schemes = schemes;
    }

    /// <summary>
    ///     Gets or sets the <see cref="IAuthenticationSchemeProvider" />.
    /// </summary>
    private IAuthenticationSchemeProvider Schemes { get; }

    /// <summary>
    ///     Invokes the middleware performing authentication.
    /// </summary>
    /// <param name="context">The <see cref="HttpContext" />.</param>
    public async Task Invoke(HttpContext context)
    {
        var endpoint = context.GetEndpoint()
                       ?? throw new InvalidOperationException("Routing Endpoint wasn't set for the current request.");
        var route = endpoint.Metadata.GetMetadata<RouteModel>();
        if (route == null)
        {
            await _next(context);
            return;
        }

        var scheme = await GetSchemeAsync(route);
        if (scheme != null)
        {
            var result = await context.AuthenticateAsync(scheme);
            if (result?.Principal != null) context.User = result.Principal;
        }

        await _next(context);
    }

    private async Task<string?> GetSchemeAsync(RouteModel route)
    {
        var scheme = route.Config.AuthorizationPolicy;
        if (scheme == null)
        {
            var defaultAuthenticate = await Schemes.GetDefaultAuthenticateSchemeAsync();
            return defaultAuthenticate?.Name;
        }

        return scheme;
    }
}