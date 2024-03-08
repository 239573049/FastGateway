namespace FastGateway.Middlewares;

public sealed class RequestSourceMiddleware(RequestSourceService requestSourceService) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var ip = context.Request.Headers["X-Forwarded-For"].ToString();

        // 获取当前请求平台
        requestSourceService.Execute(new RequestSourceEntity()
        {
            CreatedTime = DateTime.Now,
            Ip = ip,
            Host = context.Request.Host.Host,
            Platform = GetUserPlatform(context.Request.Headers.UserAgent.ToString()),
            Id = Guid.NewGuid().ToString("N"),
        });

        await next(context);
    }

    private static string GetUserPlatform(string userAgent)
    {
        if (userAgent.Contains("Windows NT"))
        {
            return "Windows";
        }

        if (userAgent.Contains("Macintosh"))
        {
            return "Mac";
        }

        return userAgent.Contains("Linux") ? "Linux" : "Unknown";
    }
}