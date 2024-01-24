namespace Gateway.Middlewares;

public class StaticFileProxyMiddleware(IContentTypeProvider contentTypeProvider) : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // 如果是非GET请求，则直接跳过, /api/gateway/开头的请求也跳过 这个是属于系统内置的接口
        if ((context.Request.Method != "GET" && StaticFileProxyService.StaticFileProxyEntityList.Count == 0) ||
            context.Request.Path.Value?.StartsWith("/api/gateway/") == true)
        {
            await next(context);
        }
        else
        {
            // 获取请求的域名
            var host = context.Request.Host.Host;
            // 获取请求的路径
            var path = context.Request.Path.Value;

            // 获取匹配的静态文件代理配置
            var staticFileProxyEntity = StaticFileProxyService.StaticFileProxyEntityList.FirstOrDefault(x =>
                x.Hosts.Any(h => h == host) && path?.StartsWith(x.Path) == true);

            // 如果没有匹配的静态文件代理配置，则找到没有配置域名的静态文件代理配置
            if (staticFileProxyEntity == null)
            {
                staticFileProxyEntity = StaticFileProxyService.StaticFileProxyEntityList.FirstOrDefault(x =>
                    x.Hosts.Length == 0 && path?.StartsWith(x.Path) == true);
            }

            // 如果没有匹配的静态文件代理配置，则直接跳过
            if (staticFileProxyEntity == null)
            {
                await next(context);
                return;
            }

            // 设置响应头
            foreach (var header in staticFileProxyEntity.ResponseHeaders)
            {
                context.Response.Headers[header.Key] = header.Value;
            }

            // 获取静态文件的路径
            var staticFilePath = staticFileProxyEntity.Root;

            if (context.Request.Path.Value == "/")
            {
                context.Request.Path = "/" + staticFileProxyEntity.Index;
            }

            // 拼接静态文件的完整路径
            var filePath = Path.Combine(staticFilePath, context.Request.Path.Value?.TrimStart('/') ?? string.Empty);

            // 如果文件不存在，则404
            if (!File.Exists(filePath))
            {
                // 尝试TryFiles
                if (staticFileProxyEntity.TryFiles.Length != 0)
                {
                    foreach (var tryFile in staticFileProxyEntity.TryFiles)
                    {
                        var tryFilePath = Path.Combine(staticFilePath, tryFile);
                        if (!File.Exists(tryFilePath)) continue;
                        filePath = tryFilePath;
                        // 跳出循环指向读取文件
                        goto readfile;
                    }
                }

                context.Response.StatusCode = 404;
                return;
            }

        readfile:

            // 获取文件的ContentType
            context.Response.ContentType = contentTypeProvider.TryGetContentType(filePath, out var contentType)
                ? contentType
                : "application/octet-stream";

            // 是否使用gzip压缩
            if (staticFileProxyEntity.GZip)
            {
                // 判断当前是否使用压缩
                var acceptEncoding = context.Request.Headers["Accept-Encoding"].ToString();
                if (!acceptEncoding.Contains("gzip"))
                {
                    await context.Response.SendFileAsync(filePath);
                    return;
                }

                context.Response.Headers.ContentEncoding = "gzip";

                // 使用压缩gzip的Stream返回
                await using var gzipStream = new GZipStream(context.Response.Body, CompressionMode.Compress);
                await using var fileStream = File.OpenRead(filePath);
                await fileStream.CopyToAsync(gzipStream);
                return;
            }

            await context.Response.SendFileAsync(filePath);
        }
    }
}