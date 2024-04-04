namespace FastGateway.Services;

public static class AcmeChallenge
{
    public static async Task Challenge(HttpContext context, string token)
    {
        if (FastContext.MemoryCache.TryGetValue(token, out var value))
        {
            context.Response.ContentType = "text/plain";

            await context.Response.WriteAsync(value.ToString());

            return;
        }

        context.Response.StatusCode = 404;
    }
}