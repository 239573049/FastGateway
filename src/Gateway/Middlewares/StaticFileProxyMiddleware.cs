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

            // 将路由前缀替换为空，只替换前缀不适用Replace方法，因为可能会出现多个相同的前缀
            context.Request.Path = context.Request.Path.Value?.TrimStart(staticFileProxyEntity.Path.ToArray());


            // 拼接静态文件的完整路径
            var fileInfo = new FileInfo(Path.Combine(staticFilePath,
                context.Request.Path.Value?.TrimStart('/') ?? string.Empty));

            Console.WriteLine(
                $"静态文件代理：{staticFileProxyEntity.Path} -> {staticFileProxyEntity.Root}  Path > {context.Request.Path} FilePath > {fileInfo.FullName} 文件是否存在：{fileInfo.Exists}");

            // 如果文件不存在，则404
            if (!fileInfo.Exists)
            {
                // 尝试TryFiles
                if (staticFileProxyEntity.TryFiles?.Length > 0)
                {
                    foreach (var tryFile in staticFileProxyEntity.TryFiles)
                    {
                        var tryFilePath = Path.Combine(staticFilePath, tryFile);
                        if (!File.Exists(tryFilePath)) continue;
                        fileInfo = new FileInfo(tryFilePath);
                        // 跳出循环指向读取文件
                        goto readfile;
                    }
                }

                context.Response.StatusCode = 404;
                return;
            }

            readfile:

            // 获取文件的ContentType
            context.Response.ContentType =
                contentTypeProvider.TryGetContentType(fileInfo.Extension, out var contentType)
                    ? contentType
                    : "application/octet-stream";

            // 判断当前是否使用压缩
            var acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();
            // 是否使用gzip压缩
            if (staticFileProxyEntity.GZip && acceptEncoding.Contains("gzip"))
            {
                context.Response.Headers.ContentEncoding = "gzip";

                // 将文件流转换为gzip流
                await using var gzipStream = new GZipStream(context.Response.Body, CompressionMode.Compress);
                await using var fileStream = File.OpenRead(fileInfo.FullName);
                await fileStream.CopyToAsync(gzipStream, 81920);
                return;
            }

            await context.Response.SendFileAsync(fileInfo.FullName, 0, fileInfo.Length, context.RequestAborted);
        }
    }
}