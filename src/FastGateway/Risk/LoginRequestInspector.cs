using System.Text;
using System.Text.Json;
using Core.Entities;
using FastGateway.Risk.Model;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace FastGateway.Risk;

/// <summary>
///     登录请求与响应的识别：读取用户名、判断客户端类型、根据上游响应判断登录成败
/// </summary>
public static class LoginRequestInspector
{
    /// <summary>只为读取用户名缓冲的请求体上限；登录表单远小于此，超出说明不是正常登录请求，直接跳过账号维度</summary>
    public const int MaxUsernameBodyBytes = 16 * 1024;

    private static readonly string[] CliUserAgents = ["curl/", "wget/", "powershell", "httpie", "hydra"];

    private static readonly string[] LibraryUserAgents =
    [
        "python-requests", "python-urllib", "python-httpx", "aiohttp", "go-http-client", "java/", "okhttp",
        "apache-httpclient", "node-fetch", "undici", "axios/", "libwww-perl", "httpclient", "postmanruntime",
        "insomnia", "scrapy"
    ];

    /// <summary>
    ///     读取请求体中的用户名字段，读取后把流位置倒回开头，转发给上游的内容不变。任何异常都视为读不到用户名
    /// </summary>
    public static async Task<string?> TryReadUsernameAsync(HttpRequest request, string field,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength is > MaxUsernameBodyBytes or 0) return null;

        var contentType = request.ContentType ?? string.Empty;
        var isJson = contentType.Contains("json", StringComparison.OrdinalIgnoreCase);
        var isForm = contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
        if (!isJson && !isForm) return null;

        request.EnableBuffering();
        try
        {
            var buffer = new byte[MaxUsernameBodyBytes + 1];
            var length = 0;
            int read;
            while (length < buffer.Length &&
                   (read = await request.Body.ReadAsync(buffer.AsMemory(length), cancellationToken)) > 0)
                length += read;

            if (length > MaxUsernameBodyBytes) return null;

            var body = buffer.AsSpan(0, length);
            return isJson ? ReadJsonField(body, field) : ReadFormField(body, field);
        }
        catch (Exception e) when (e is IOException or BadHttpRequestException)
        {
            return null;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    public static string? ReadJsonField(ReadOnlySpan<byte> json, string field)
    {
        try
        {
            var reader = new Utf8JsonReader(json, new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject) return null;

            while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
            {
                var matches = string.Equals(reader.GetString(), field, StringComparison.OrdinalIgnoreCase);
                if (!reader.Read()) return null;

                if (matches)
                    return reader.TokenType switch
                    {
                        JsonTokenType.String => reader.GetString(),
                        JsonTokenType.Number => Encoding.UTF8.GetString(reader.ValueSpan),
                        _ => null
                    };

                reader.Skip();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    public static string? ReadFormField(ReadOnlySpan<byte> body, string field)
    {
        var form = QueryHelpers.ParseQuery(Encoding.UTF8.GetString(body));
        foreach (var (key, value) in form)
        {
            if (string.Equals(key, field, StringComparison.OrdinalIgnoreCase))
                return value.Count > 0 ? value[0] : null;
        }

        return null;
    }

    /// <summary>
    ///     脚本类客户端：UA 为空或是常见 HTTP 库/工具，或同时缺少浏览器必带的 Sec-Fetch-Mode 与 Accept-Language
    /// </summary>
    public static bool IsNonBrowser(IHeaderDictionary headers)
    {
        return ClassifyClient(headers.UserAgent.ToString()) is ClientKinds.Empty or ClientKinds.CliTool
                   or ClientKinds.ScriptLibrary ||
               !headers.ContainsKey("Sec-Fetch-Mode") && !headers.ContainsKey(HeaderNames.AcceptLanguage);
    }

    /// <summary>
    ///     把 UA 归入固定枚举；送往模型的只有这个分类，不含 UA 原文
    /// </summary>
    public static string ClassifyClient(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return ClientKinds.Empty;
        if (CliUserAgents.Any(x => userAgent.Contains(x, StringComparison.OrdinalIgnoreCase)))
            return ClientKinds.CliTool;
        if (LibraryUserAgents.Any(x => userAgent.Contains(x, StringComparison.OrdinalIgnoreCase)))
            return ClientKinds.ScriptLibrary;
        return userAgent.StartsWith("Mozilla/", StringComparison.Ordinal) ? ClientKinds.Browser : ClientKinds.Unknown;
    }

    public static LoginSignals CaptureSignals(IHeaderDictionary headers)
    {
        return new LoginSignals(
            ClassifyClient(headers.UserAgent.ToString()),
            headers.ContainsKey("Sec-Fetch-Mode"),
            headers.ContainsKey(HeaderNames.AcceptLanguage),
            IsNonBrowser(headers));
    }

    /// <summary>
    ///     浏览器页面导航（而非接口调用或静态资源）：只有这类请求适合直接返回验证页
    /// </summary>
    public static bool IsNavigation(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)) return false;

        var mode = request.Headers["Sec-Fetch-Mode"].ToString();
        if (mode.Length > 0) return string.Equals(mode, "navigate", StringComparison.OrdinalIgnoreCase);

        return request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     结果头优先于状态码；3xx、5xx 等无法判断的响应不计入
    /// </summary>
    public static LoginOutcome DetectOutcome(int statusCode, IHeaderDictionary responseHeaders, LoginEndpointRule rule)
    {
        if (!string.IsNullOrEmpty(rule.ResultHeader) &&
            responseHeaders.TryGetValue(rule.ResultHeader, out var resultValue) && resultValue.Count > 0)
        {
            var value = resultValue.ToString().Trim();
            if (value.Equals("fail", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("failure", StringComparison.OrdinalIgnoreCase))
                return LoginOutcome.Failure;
            if (value.Equals("success", StringComparison.OrdinalIgnoreCase))
                return LoginOutcome.Success;
        }

        if (rule.FailureStatusCodes.Contains(statusCode)) return LoginOutcome.Failure;
        if (statusCode is >= 200 and < 300 && string.IsNullOrEmpty(rule.ResultHeader)) return LoginOutcome.Success;
        return LoginOutcome.Unknown;
    }
}
