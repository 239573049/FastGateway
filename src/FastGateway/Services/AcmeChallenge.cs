namespace FastGateway.Services;

public static class AcmeChallenge
{
    private static readonly Dictionary<string, string> Tokens = new();

    public static void AddToken(string token, string value)
    {
        Tokens[token] = value;
    }

    public static async Task Challenge(HttpContext context, string token)
    {
        if (Tokens.TryGetValue(token, out var token1))
        {
            context.Response.ContentType = "text/plain";

            await context.Response.WriteAsync(token1);

            // Tokens.Remove(token);
        }
        else
        {
            context.Response.StatusCode = 404;
        }
    }
}