using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastGateway.Infrastructure;

namespace FastGateway.Risk.Model;

public enum JevErrorKind
{
    None,
    Timeout,
    Unauthorized,
    RateLimited,
    InvalidRequest,
    ServerError,
    Network,
    BadResponse
}

public sealed record JevResult(
    JevErrorKind Error,
    string? Message,
    JevResponse? Response,
    long LatencyMs,
    TimeSpan? RetryAfter = null)
{
    public bool Success => Error == JevErrorKind.None && Response != null;
}

/// <summary>
///     TypeSafe System One API 客户端。不记录请求、响应与 Key；所有失败都以 <see cref="JevResult" /> 返回，不抛异常
/// </summary>
public sealed class JevClient(IHttpClientFactory httpClientFactory)
{
    public const string HttpClientName = "FastGateway.RiskModel";

    public async Task<JevResult> EvaluateAsync(RiskModelProviderSettings settings, JevRequest request,
        CancellationToken cancellationToken)
    {
        // 先序列化再发送：带 Content-Length，避免部分网关/代理不接受分块传输的请求体
        var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(request, AppJsonContext.Default.JevRequest));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var result = await SendAsync(settings, HttpMethod.Post, "/v1/systemone", content, cancellationToken);
        if (result.Error != JevErrorKind.None) return result.Result;

        try
        {
            var response = JsonSerializer.Deserialize(result.Body, AppJsonContext.Default.JevResponse);
            return response?.Answers == null
                ? result.Result with { Error = JevErrorKind.BadResponse, Message = "响应缺少 answers" }
                : result.Result with { Response = response };
        }
        catch (JsonException)
        {
            return result.Result with { Error = JevErrorKind.BadResponse, Message = "响应不是有效的 JSON" };
        }
    }

    public async Task<(JevResult Result, List<JevModelInfo> Models)> ListModelsAsync(
        RiskModelProviderSettings settings, CancellationToken cancellationToken)
    {
        var result = await SendAsync(settings, HttpMethod.Get, "/v1/models", null, cancellationToken);
        if (result.Error != JevErrorKind.None) return (result.Result, []);

        try
        {
            var list = JsonSerializer.Deserialize(result.Body, AppJsonContext.Default.JevModelList);
            return (result.Result, list?.Models ?? []);
        }
        catch (JsonException)
        {
            return (result.Result with { Error = JevErrorKind.BadResponse, Message = "响应不是有效的 JSON" }, []);
        }
    }

    private async Task<(JevErrorKind Error, JevResult Result, byte[] Body)> SendAsync(
        RiskModelProviderSettings settings, HttpMethod method, string path, HttpContent? content,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.TimeoutMs);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(method, settings.Endpoint + path) { Content = content };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await httpClientFactory.CreateClient(HttpClientName)
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var body = await response.Content.ReadAsByteArrayAsync(timeout.Token);
            var latency = stopwatch.ElapsedMilliseconds;

            if (response.IsSuccessStatusCode)
                return (JevErrorKind.None, new JevResult(JevErrorKind.None, null, null, latency), body);

            var (kind, message) = response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => (JevErrorKind.Unauthorized,
                    "API Key 无效或无权访问"),
                HttpStatusCode.TooManyRequests => (JevErrorKind.RateLimited, "触发提供商限流"),
                HttpStatusCode.UnprocessableEntity or HttpStatusCode.BadRequest or HttpStatusCode.NotFound =>
                    (JevErrorKind.InvalidRequest, $"请求被拒绝（{(int)response.StatusCode}），请检查模型名与服务地址"),
                _ => (JevErrorKind.ServerError, $"提供商返回 {(int)response.StatusCode}")
            };
            var retryAfter = response.Headers.RetryAfter?.Delta ??
                             (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
            return (kind, new JevResult(kind, message, null, latency, retryAfter), body);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failure(JevErrorKind.Timeout, $"超过 {settings.TimeoutMs} 毫秒未响应");
        }
        catch (HttpRequestException e)
        {
            return Failure(JevErrorKind.Network, e.HttpRequestError switch
            {
                HttpRequestError.NameResolutionError => "无法解析服务地址",
                HttpRequestError.ConnectionError => "无法连接服务地址",
                HttpRequestError.SecureConnectionError => "TLS 握手失败",
                _ => "网络错误"
            });
        }

        (JevErrorKind, JevResult, byte[]) Failure(JevErrorKind kind, string message)
        {
            return (kind, new JevResult(kind, message, null, stopwatch.ElapsedMilliseconds), []);
        }
    }
}

public sealed class JevRequest
{
    [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;

    [JsonPropertyName("state")] public RiskFeatureState State { get; set; } = new();

    [JsonPropertyName("questions")] public Dictionary<string, JevQuestion> Questions { get; set; } = new();
}

public sealed class JevQuestion
{
    [JsonPropertyName("type")] public string Type { get; set; } = "noul";

    [JsonPropertyName("instructions")] public string Instructions { get; set; } = string.Empty;
}

public sealed class JevResponse
{
    [JsonPropertyName("model")] public string? Model { get; set; }

    [JsonPropertyName("answers")] public Dictionary<string, JevAnswer>? Answers { get; set; }

    [JsonPropertyName("usage")] public JevUsage? Usage { get; set; }
}

public sealed class JevAnswer
{
    [JsonPropertyName("type")] public string? Type { get; set; }

    /// <summary>noul 题的结论为真的概率（0-1）</summary>
    [JsonPropertyName("noul")] public double? Noul { get; set; }
}

public sealed class JevUsage
{
    [JsonPropertyName("input_tokens")] public long InputTokens { get; set; }

    [JsonPropertyName("output_tokens")] public long OutputTokens { get; set; }
}

public sealed class JevModelList
{
    [JsonPropertyName("models")] public List<JevModelInfo>? Models { get; set; }
}

public sealed class JevModelInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")] public string? Description { get; set; }

    [JsonPropertyName("release_date")] public string? ReleaseDate { get; set; }
}
