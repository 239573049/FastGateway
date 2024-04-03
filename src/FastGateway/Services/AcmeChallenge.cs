namespace FastGateway.Services;

public static class AcmeChallenge
{
    public static async Task Challenge(HttpContext context, IMemoryCache cache, string token)
    {
        if (cache.TryGetValue(token, out var value))
        {
            context.Response.ContentType = "text/plain";

            await context.Response.WriteAsync(value.ToString());

            return;
        }

        context.Response.StatusCode = 404;
    }
}