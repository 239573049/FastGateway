using System.Net;
using System.Security.Cryptography;
using Core.Entities;
using FastGateway.Cluster;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Services;
using FastGateway.Services.Statistics;
using Yarp.ReverseProxy.Model;

namespace FastGateway.Middleware;

public sealed class BotProtectionMiddleware
{
    /// <summary>网关自身的内部端点（机器人验证、登录风控验证），不受业务路由保护规则拦截</summary>
    public const string InternalPathPrefix = "/__fastgateway";

    public const string ChallengePath = "/__fastgateway/bot/challenge";
    public const string VerifyPath = "/__fastgateway/bot/verify";
    public const string BusinessAction = "business-access";

    private readonly RequestDelegate _next;
    private readonly BotProtectionService _botProtection;

    public BotProtectionMiddleware(RequestDelegate next, BotProtectionService botProtection)
    {
        _next = next;
        _botProtection = botProtection;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (ShouldBypass(context))
        {
            await _next(context);
            return;
        }

        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<RouteModel>()?.Config.Metadata;
        if (metadata == null ||
            !metadata.TryGetValue(BotProtectionService.ProtectionMetadataKey, out var enabled) ||
            !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (IsTrustedRelay(context))
        {
            await _next(context);
            return;
        }

        var routeId = GetMetadata(metadata, BotProtectionService.RouteIdMetadataKey);
        var routePath = GetMetadata(metadata, BotProtectionService.RoutePathMetadataKey) ?? "/";
        if (string.IsNullOrWhiteSpace(routeId))
        {
            MarkBotChallenge(context);
            await WriteResponseAsync(context, StatusCodes.Status503ServiceUnavailable,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = "机器人保护配置无效",
                    ErrorCode = "bot_protection_misconfigured"
                });
            return;
        }

        if (!_botProtection.IsClearanceConfigured || !IsSecureRequest(context.Request))
        {
            MarkBotChallenge(context);
            await WriteResponseAsync(context, StatusCodes.Status503ServiceUnavailable,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = IsSecureRequest(context.Request)
                        ? "机器人保护服务未正确配置"
                        : "机器人保护需要 HTTPS",
                    ErrorCode = "bot_protection_unavailable"
                });
            return;
        }

        var clientIp = ClientIpHelper.GetClientIp(context);
        if (_botProtection.ValidateClearance(
                context.Request.Cookies[BotProtectionService.ClearanceCookieName],
                context.Request.Host.Host,
                routeId,
                routePath,
                clientIp))
        {
            await _next(context);
            return;
        }

        var state = _botProtection.CreateChallengeState(
            context.Request.Host.Host,
            routeId,
            GetReturnUrl(context),
            routePath,
            BusinessAction);
        if (string.IsNullOrEmpty(state))
        {
            MarkBotChallenge(context);
            await WriteResponseAsync(context, StatusCodes.Status503ServiceUnavailable,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = "机器人保护服务未正确配置",
                    ErrorCode = "bot_protection_unavailable"
                });
            return;
        }

        var challengeUrl = BuildChallengeUrl(state);
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        {
            MarkBotChallenge(context);
            await BotProtectionMiddlewareExtensions.WriteChallengePageAsync(context, _botProtection, state);
            return;
        }

        MarkBotChallenge(context);
        await WriteResponseAsync(context, StatusCodes.Status403Forbidden,
            new BotChallengeResponse
            {
                Success = false,
                RedirectUrl = challengeUrl,
                Message = "请先完成人机验证后重试",
                ErrorCode = "bot_challenge_required"
            });
    }

    private static bool ShouldBypass(HttpContext context)
    {
        var path = context.Request.Path;
        return HttpMethods.IsOptions(context.Request.Method) ||
               path.StartsWithSegments("/.well-known/acme-challenge", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments("/internal/gateway", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWithSegments(InternalPathPrefix, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsTrustedRelay(HttpContext context)
    {
        return context.Request.Headers.ContainsKey(ClusterRelay.RelayCountHeader) &&
               ClusterRelay.ValidateRelayToken(context.Request.Headers[ClusterRelay.RelayTokenHeader]);
    }

    internal static bool IsSecureRequest(HttpRequest request)
    {
        if (request.IsHttps) return true;
        if (!request.Headers.TryGetValue("X-Forwarded-Proto", out var forwardedProto)) return false;

        var value = forwardedProto.ToString();
        var separatorIndex = value.IndexOf(',');
        var proto = separatorIndex >= 0 ? value.AsSpan(0, separatorIndex) : value.AsSpan();
        return proto.Trim().Equals("https", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetMetadata(IReadOnlyDictionary<string, string> metadata, string key)
    {
        return metadata.TryGetValue(key, out var value) ? value : null;
    }

    private static string GetReturnUrl(HttpContext context)
    {
        return context.Request.PathBase + context.Request.Path + context.Request.QueryString;
    }

    private static string BuildChallengeUrl(string state)
    {
        return $"{ChallengePath}?state={Uri.EscapeDataString(state)}";
    }

    private static async Task WriteResponseAsync(HttpContext context, int statusCode, BotChallengeResponse payload)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Vary = "Cookie";
        await context.Response.WriteAsJsonAsync(payload, AppJsonContext.Default.BotChallengeResponse);
    }

    private static void MarkBotChallenge(HttpContext context)
    {
        context.Items[StatisticsCollector.BlockReasonKey] = (byte)BlockReason.BotChallenge;
    }

    private static string CreateNonce()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

public static class BotProtectionMiddlewareExtensions
{
    public static IApplicationBuilder UseBotProtection(this IApplicationBuilder app)
    {
        return app.UseMiddleware<BotProtectionMiddleware>();
    }

    public static IEndpointRouteBuilder MapBotProtectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(BotProtectionMiddleware.ChallengePath, HandleChallengeAsync);
        endpoints.MapPost(BotProtectionMiddleware.VerifyPath, HandleVerifyAsync);
        return endpoints;
    }

    private static async Task HandleChallengeAsync(HttpContext context, BotProtectionService botProtection)
    {
        var stateValue = context.Request.Query["state"].ToString();
        if (!botProtection.TryReadChallengeState(stateValue, context.Request.Host.Host, out var state))
        {
            await WriteEndpointResponseAsync(context, StatusCodes.Status400BadRequest,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = "挑战已失效，请重新发起请求",
                    ErrorCode = "bot_challenge_invalid"
                });
            return;
        }

        if (!botProtection.IsClearanceConfigured || !BotProtectionMiddleware.IsSecureRequest(context.Request))
        {
            await WriteEndpointResponseAsync(context, StatusCodes.Status503ServiceUnavailable,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = "机器人保护服务暂时不可用",
                    ErrorCode = "bot_protection_unavailable"
                });
            return;
        }

        await WriteChallengePageAsync(context, botProtection, stateValue);
    }

    private static async Task HandleVerifyAsync(HttpContext context, BotProtectionService botProtection,
        CancellationToken cancellationToken)
    {
        var clientIp = ClientIpHelper.GetClientIp(context);
        if (!botProtection.TryConsumeVerifyRequest(clientIp))
        {
            await WriteEndpointResponseAsync(context, StatusCodes.Status429TooManyRequests,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = "验证请求过于频繁，请稍后重试",
                    ErrorCode = "bot_verify_rate_limited"
                });
            return;
        }

        var request = await ReadVerifyRequestAsync(context.Request, cancellationToken);
        if (request == null ||
            !botProtection.TryReadChallengeState(request.State, context.Request.Host.Host, out var state))
        {
            await WriteEndpointResponseAsync(context, StatusCodes.Status400BadRequest,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = "挑战已失效，请重新发起请求",
                    ErrorCode = "bot_challenge_invalid"
                });
            return;
        }

        if (!botProtection.IsClearanceConfigured || !BotProtectionMiddleware.IsSecureRequest(context.Request))
        {
            await WriteEndpointResponseAsync(context, StatusCodes.Status503ServiceUnavailable,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = "机器人保护服务暂时不可用",
                    ErrorCode = "bot_protection_unavailable"
                });
            return;
        }

        var verification = await botProtection.VerifyTurnstileAsync(
            request.Token,
            state.Action,
            context.Request.Host.Host,
            clientIp,
            cancellationToken);
        if (verification.Status == BotVerificationStatus.Unavailable)
        {
            await WriteEndpointResponseAsync(context, StatusCodes.Status503ServiceUnavailable,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = "人机验证服务暂时不可用，请稍后重试",
                    ErrorCode = "bot_verify_unavailable"
                });
            return;
        }

        if (verification.Status != BotVerificationStatus.Success)
        {
            await WriteEndpointResponseAsync(context, StatusCodes.Status403Forbidden,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = "人机验证失败，请重试",
                    ErrorCode = "bot_verify_rejected"
                });
            return;
        }

        if (!botProtection.TryConsumeChallengeState(request.State, context.Request.Host.Host, out state))
        {
            await WriteEndpointResponseAsync(context, StatusCodes.Status400BadRequest,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = "挑战已失效，请重新发起请求",
                    ErrorCode = "bot_challenge_invalid"
                });
            return;
        }

        var clearance = botProtection.CreateClearance(
            context.Request.Host.Host,
            state.RouteId,
            state.RoutePath,
            clientIp);
        if (string.IsNullOrEmpty(clearance))
        {
            await WriteEndpointResponseAsync(context, StatusCodes.Status503ServiceUnavailable,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = "机器人保护服务未正确配置",
                    ErrorCode = "bot_protection_unavailable"
                });
            return;
        }

        botProtection.AppendClearanceCookie(context.Response, clearance, state.RoutePath);
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Location = state.ReturnUrl;
    }

    internal static async Task<BotVerifyRequest?> ReadVerifyRequestAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true)
            return await request.ReadFromJsonAsync(AppJsonContext.Default.BotVerifyRequest, cancellationToken);

        if (!request.HasFormContentType) return null;
        var form = await request.ReadFormAsync(cancellationToken);
        return new BotVerifyRequest
        {
            Token = form["token"].ToString(),
            State = form["state"].ToString()
        };
    }

    internal static async Task WriteEndpointResponseAsync(HttpContext context, int statusCode,
        BotChallengeResponse payload)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(payload, AppJsonContext.Default.BotChallengeResponse);
    }

    internal static async Task WriteChallengePageAsync(HttpContext context, BotProtectionService botProtection,
        string state, string verifyPath = BotProtectionMiddleware.VerifyPath,
        string action = BotProtectionMiddleware.BusinessAction)
    {
        var nonce = CreateNonce();
        var siteKey = botProtection.GetAdminChallengeConfig().SiteKey;
        var encodedState = WebUtility.HtmlEncode(state);
        var encodedSiteKey = WebUtility.HtmlEncode(siteKey);
        var encodedVerifyPath = WebUtility.HtmlEncode(verifyPath);
        var encodedAction = WebUtility.HtmlEncode(action);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.ContentSecurityPolicy =
            $"default-src 'none'; script-src 'nonce-{nonce}' https://challenges.cloudflare.com; " +
            "frame-src https://challenges.cloudflare.com; connect-src 'self' https://challenges.cloudflare.com; " +
            "style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";

        var html = """
            <!doctype html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>正在验证访问</title>
              <style>body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #f8fafc; color: #0f172a; font: 16px system-ui, sans-serif; } main { width: min(92vw, 30rem); padding: 2rem; border: 1px solid #e2e8f0; border-radius: 1rem; background: white; box-shadow: 0 15px 45px rgba(15, 23, 42, .12); } h1 { margin-top: 0; font-size: 1.35rem; } p { color: #475569; line-height: 1.6; } #turnstile { min-height: 65px; }</style>
            </head>
            <body>
              <main>
                <h1>请完成安全验证</h1>
                <p>验证完成后将自动返回原页面。</p>
                <div id="turnstile" data-site-key="__SITE_KEY__" data-state="__STATE__" data-action="__ACTION__"></div>
                <form id="verify-form" method="post" action="__VERIFY_PATH__" hidden></form>
              </main>
              <script nonce="__NONCE__">
                (() => {
                  const root = document.getElementById('turnstile');
                  const form = document.getElementById('verify-form');
                  if (!root || !form) return;
                  const render = () => {
                    if (!window.turnstile) return;
                    window.turnstile.render(root, { sitekey: root.dataset.siteKey, action: root.dataset.action, callback: (token) => {
                      const tokenInput = document.createElement('input'); tokenInput.name = 'token'; tokenInput.type = 'hidden'; tokenInput.value = token;
                      const stateInput = document.createElement('input'); stateInput.name = 'state'; stateInput.type = 'hidden'; stateInput.value = root.dataset.state || '';
                      form.append(tokenInput, stateInput); form.submit();
                    } });
                  };
                  if (window.turnstile) render(); else { const script = document.createElement('script'); script.src = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit'; script.async = true; script.defer = true; script.onload = render; document.head.appendChild(script); }
                })();
              </script>
            </body>
            </html>
            """;

        html = html
            .Replace("__SITE_KEY__", encodedSiteKey, StringComparison.Ordinal)
            .Replace("__STATE__", encodedState, StringComparison.Ordinal)
            .Replace("__VERIFY_PATH__", encodedVerifyPath, StringComparison.Ordinal)
            .Replace("__ACTION__", encodedAction, StringComparison.Ordinal)
            .Replace("__NONCE__", nonce, StringComparison.Ordinal);

        await context.Response.WriteAsync(html);
    }

    private static string CreateNonce()
    {
        Span<byte> bytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
