using System.Globalization;
using System.Net;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Middleware;
using FastGateway.Services;
using FastGateway.Services.Statistics;
using Yarp.ReverseProxy.Model;

namespace FastGateway.Risk;

/// <summary>
///     业务路由的登录风控：在登录请求转发前打分决策，在上游响应开始时识别登录结果并计数。
///     只作用于配置了 <see cref="RiskProtection" /> 的路由。
/// </summary>
public sealed class RiskGuardMiddleware
{
    public const string ChallengePath = "/__fastgateway/risk/challenge";
    public const string VerifyPath = "/__fastgateway/risk/verify";
    public const string ChallengeAction = "risk-login";
    public const string ClearanceCookieName = "__FastGateway_RiskClearance";

    private readonly RequestDelegate _next;
    private readonly BotProtectionService _botProtection;
    private readonly RiskEngine _engine = RiskEngine.Shared;

    public RiskGuardMiddleware(RequestDelegate next, BotProtectionService botProtection)
    {
        _next = next;
        _botProtection = botProtection;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        if (HttpMethods.IsOptions(request.Method) ||
            request.Path.StartsWithSegments(BotProtectionMiddleware.InternalPathPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var routeId = context.GetEndpoint()?.Metadata.GetMetadata<RouteModel>()?.Config.RouteId;
        var protection = routeId == null ? null : _engine.GetRouteProtection(routeId);
        if (protection == null || BotProtectionMiddleware.IsTrustedRelay(context))
        {
            await _next(context);
            return;
        }

        var ip = ClientIpHelper.GetClientIp(context);
        var host = request.Host.Host;
        var path = request.Path.Value ?? "/";
        var rule = protection.FindRule(request.Method, path);

        if (rule == null)
        {
            if (protection.BlockScope == RiskBlockScope.WholeRoute && _engine.GetBlockRemaining(ip) is { } blocked)
            {
                await WriteBlockedAsync(context, blocked);
                return;
            }

            // 被标记的来源刷新页面时先完成验证，拿到 clearance 后再回到登录页提交，
            // 这样业务前端不需要处理 risk_challenge_required 也能走通
            if (LoginRequestInspector.IsNavigation(request) && _engine.HasSigningKey)
            {
                var assessment = _engine.Assess(ip, null, nonBrowser: false);
                if (assessment.Score >= _engine.Settings.ChallengeThreshold &&
                    !_engine.TryUseClearance(request.Cookies[ClearanceCookieName], host, routeId!, ip,
                        consume: false))
                {
                    var (provider, difficulty) =
                        _engine.ChooseChallenge(assessment.Score, IsTurnstileAvailable(context));
                    var state = _engine.CreateChallengeState(host, routeId!,
                        request.PathBase + request.Path + request.QueryString, ip, provider, difficulty);
                    if (state != null)
                    {
                        _engine.RecordDecision(ip, host, path, RiskAction.Challenge, assessment);
                        MarkBlocked(context, BlockReason.RiskChallenge);
                        await RiskGuardMiddlewareExtensions.WriteChallengePageAsync(context, _botProtection,
                            state, provider, difficulty);
                        return;
                    }
                }
            }

            await _next(context);
            return;
        }

        if (_engine.GetBlockRemaining(ip) is { } remaining)
        {
            await WriteBlockedAsync(context, remaining);
            return;
        }

        string? accountKey = null, accountHint = null;
        if (!string.IsNullOrWhiteSpace(rule.UsernameField))
        {
            var username = await LoginRequestInspector.TryReadUsernameAsync(request, rule.UsernameField,
                context.RequestAborted);
            if (!string.IsNullOrWhiteSpace(username))
            {
                accountKey = _engine.HashAccount(routeId!, username);
                accountHint = RiskEngine.MaskUsername(username);
            }
        }

        var signals = LoginRequestInspector.CaptureSignals(request.Headers);
        var loginAssessment = _engine.Assess(ip, accountKey, signals.NonBrowser);
        switch (_engine.Decide(loginAssessment.Score, _engine.HasSigningKey))
        {
            case RiskAction.Block:
                await WriteBlockedAsync(context, _engine.BlockIp(ip, loginAssessment, host, path));
                return;

            case RiskAction.Challenge when !_engine.TryUseClearance(request.Cookies[ClearanceCookieName], host,
                routeId!, ip, consume: true):
                var (provider, difficulty) =
                    _engine.ChooseChallenge(loginAssessment.Score, IsTurnstileAvailable(context));
                var state = _engine.CreateChallengeState(host, routeId!, GetReturnUrl(request), ip, provider,
                    difficulty);
                _engine.RecordDecision(ip, host, path, RiskAction.Challenge, loginAssessment);
                MarkBlocked(context, BlockReason.RiskChallenge);
                await WriteJsonAsync(context, StatusCodes.Status403Forbidden, new BotChallengeResponse
                {
                    Success = false,
                    RedirectUrl = state == null ? null : $"{ChallengePath}?state={Uri.EscapeDataString(state)}",
                    Message = "请先完成人机验证后重试",
                    ErrorCode = "risk_challenge_required"
                });
                return;
        }

        context.Response.OnStarting(static state => OnLoginResponseStartingAsync((LoginObservation)state),
            new LoginObservation(context, _engine, rule, ip, host, accountKey, accountHint, signals));

        await _next(context);
    }

    private static async Task OnLoginResponseStartingAsync(LoginObservation observation)
    {
        var response = observation.Context.Response;
        var outcome = LoginRequestInspector.DetectOutcome(response.StatusCode, response.Headers, observation.Rule);
        if (!string.IsNullOrEmpty(observation.Rule.ResultHeader))
            response.Headers.Remove(observation.Rule.ResultHeader);

        var failures = observation.Engine.RecordLoginResult(observation.Ip, observation.AccountKey,
            observation.AccountHint, outcome, observation.Host, observation.Signals);

        // 在响应真正发出前延迟：失败越多，攻击者每次尝试等待越久
        if (outcome == LoginOutcome.Failure && observation.Engine.Settings.FailureDelay)
            await LoginFailureDelay.DelayAsync(LoginFailureDelay.For(failures), TimeProvider.System,
                observation.Context.RequestAborted);
    }

    /// <summary>
    ///     Turnstile 只在 HTTPS 下使用，与现有机器人保护一致；PoW 不依赖外部服务，明文 HTTP 也可用
    /// </summary>
    private bool IsTurnstileAvailable(HttpContext context)
    {
        return _botProtection.IsTurnstileConfigured && BotProtectionMiddleware.IsSecureRequest(context.Request);
    }

    /// <summary>
    ///     登录接口通常由页面内脚本调用，验证完成后应回到发起登录的页面而不是接口地址
    /// </summary>
    private static string GetReturnUrl(HttpRequest request)
    {
        if (Uri.TryCreate(request.Headers.Referer.ToString(), UriKind.Absolute, out var referer) &&
            string.Equals(referer.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase))
            return referer.PathAndQuery;

        return "/";
    }

    private static Task WriteBlockedAsync(HttpContext context, TimeSpan remaining)
    {
        MarkBlocked(context, BlockReason.RiskBlock);
        context.Response.Headers.RetryAfter =
            ((int)Math.Ceiling(remaining.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        return WriteJsonAsync(context, StatusCodes.Status429TooManyRequests, new BotChallengeResponse
        {
            Success = false,
            Message = "登录尝试过于频繁，请稍后重试",
            ErrorCode = "risk_blocked"
        });
    }

    private static Task WriteJsonAsync(HttpContext context, int statusCode, BotChallengeResponse payload)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        return context.Response.WriteAsJsonAsync(payload, AppJsonContext.Default.BotChallengeResponse);
    }

    private static void MarkBlocked(HttpContext context, BlockReason reason)
    {
        context.Items[StatisticsCollector.BlockReasonKey] = (byte)reason;
    }

    private sealed record LoginObservation(
        HttpContext Context,
        RiskEngine Engine,
        LoginEndpointRule Rule,
        string Ip,
        string Host,
        string? AccountKey,
        string? AccountHint,
        LoginSignals Signals);
}

public static class RiskGuardMiddlewareExtensions
{
    public static IApplicationBuilder UseRiskGuard(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RiskGuardMiddleware>();
    }

    public static IEndpointRouteBuilder MapRiskGuardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(RiskGuardMiddleware.ChallengePath, HandleChallengeAsync);
        endpoints.MapPost(RiskGuardMiddleware.VerifyPath, HandleVerifyAsync);
        return endpoints;
    }

    private static async Task HandleChallengeAsync(HttpContext context, BotProtectionService botProtection)
    {
        var engine = RiskEngine.Shared;
        var stateValue = context.Request.Query["state"].ToString();
        var ip = ClientIpHelper.GetClientIp(context);

        if (!engine.TryReadChallengeState(stateValue, context.Request.Host.Host, ip, out var state))
        {
            await BotProtectionMiddlewareExtensions.WriteEndpointResponseAsync(context,
                StatusCodes.Status400BadRequest, InvalidChallenge());
            return;
        }

        if (state.Provider == RiskSettings.ProviderTurnstile && !IsTurnstileAvailable(context, botProtection))
        {
            await BotProtectionMiddlewareExtensions.WriteEndpointResponseAsync(context,
                StatusCodes.Status503ServiceUnavailable, Unavailable());
            return;
        }

        await WriteChallengePageAsync(context, botProtection, stateValue, state.Provider, state.Difficulty);
    }

    internal static Task WriteChallengePageAsync(HttpContext context, BotProtectionService botProtection,
        string state, string provider, int difficulty)
    {
        return provider == RiskSettings.ProviderPow
            ? WritePowPageAsync(context, state, difficulty)
            : BotProtectionMiddlewareExtensions.WriteChallengePageAsync(context, botProtection, state,
                RiskGuardMiddleware.VerifyPath, RiskGuardMiddleware.ChallengeAction);
    }

    private static async Task HandleVerifyAsync(HttpContext context, BotProtectionService botProtection,
        CancellationToken cancellationToken)
    {
        var engine = RiskEngine.Shared;
        var ip = ClientIpHelper.GetClientIp(context);
        var host = context.Request.Host.Host;

        if (!botProtection.TryConsumeVerifyRequest(ip))
        {
            await BotProtectionMiddlewareExtensions.WriteEndpointResponseAsync(context,
                StatusCodes.Status429TooManyRequests, new BotChallengeResponse
                {
                    Success = false,
                    Message = "验证请求过于频繁，请稍后重试",
                    ErrorCode = "bot_verify_rate_limited"
                });
            return;
        }

        var request = await BotProtectionMiddlewareExtensions.ReadVerifyRequestAsync(context.Request,
            cancellationToken);
        if (request == null || !engine.TryReadChallengeState(request.State, host, ip, out var state))
        {
            await BotProtectionMiddlewareExtensions.WriteEndpointResponseAsync(context,
                StatusCodes.Status400BadRequest, InvalidChallenge());
            return;
        }

        if (state.Provider == RiskSettings.ProviderPow)
        {
            if (!ProofOfWork.Verify(state.Nonce, request.Token, state.Difficulty))
            {
                await BotProtectionMiddlewareExtensions.WriteEndpointResponseAsync(context,
                    StatusCodes.Status403Forbidden, new BotChallengeResponse
                    {
                        Success = false,
                        Message = "安全验证未通过，请刷新页面重试",
                        ErrorCode = "risk_pow_rejected"
                    });
                return;
            }
        }
        else if (!IsTurnstileAvailable(context, botProtection))
        {
            await BotProtectionMiddlewareExtensions.WriteEndpointResponseAsync(context,
                StatusCodes.Status503ServiceUnavailable, Unavailable());
            return;
        }
        else if (await botProtection.VerifyTurnstileAsync(request.Token, RiskGuardMiddleware.ChallengeAction,
                     host, ip, cancellationToken) is { Status: not BotVerificationStatus.Success } verification)
        {
            var unavailable = verification.Status is BotVerificationStatus.Unavailable
                or BotVerificationStatus.Misconfigured;
            await BotProtectionMiddlewareExtensions.WriteEndpointResponseAsync(context,
                unavailable ? StatusCodes.Status503ServiceUnavailable : StatusCodes.Status403Forbidden,
                new BotChallengeResponse
                {
                    Success = false,
                    Message = unavailable ? "人机验证服务暂时不可用，请稍后重试" : "人机验证失败，请重试",
                    ErrorCode = unavailable ? "bot_verify_unavailable" : "bot_verify_rejected"
                });
            return;
        }

        var clearance = engine.TryConsumeChallengeState(state)
            ? engine.IssueClearance(host, state.RouteId, ip)
            : null;
        if (clearance == null)
        {
            await BotProtectionMiddlewareExtensions.WriteEndpointResponseAsync(context,
                StatusCodes.Status400BadRequest, InvalidChallenge());
            return;
        }

        // clearance 绑定 IP 且只有几次登录额度，明文 HTTP 站点也允许下发（否则 PoW 在 HTTP 下无法生效）
        context.Response.Cookies.Append(RiskGuardMiddleware.ClearanceCookieName, clearance, new CookieOptions
        {
            HttpOnly = true,
            Secure = BotProtectionMiddleware.IsSecureRequest(context.Request),
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = TimeSpan.FromMinutes(engine.Settings.ClearanceMinutes),
            Path = "/"
        });
        engine.RecordChallengePassed(ip);
        context.Response.StatusCode = StatusCodes.Status303SeeOther;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Location = state.ReturnUrl;
    }

    private static bool IsTurnstileAvailable(HttpContext context, BotProtectionService botProtection)
    {
        return botProtection.IsTurnstileConfigured && BotProtectionMiddleware.IsSecureRequest(context.Request);
    }

    private static async Task WritePowPageAsync(HttpContext context, string state, int difficulty)
    {
        var nonce = SignedToken.CreateNonce();
        var challenge = RiskEngine.Shared.TryReadChallengeState(state, context.Request.Host.Host,
            ClientIpHelper.GetClientIp(context), out var payload)
            ? payload.Nonce
            : string.Empty;

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.ContentSecurityPolicy =
            $"default-src 'none'; script-src 'nonce-{nonce}'; worker-src blob:; style-src 'unsafe-inline'; " +
            "form-action 'self'; base-uri 'none'; frame-ancestors 'none'";

        var html = """
            <!doctype html>
            <html lang="zh-CN">
            <head>
              <meta charset="utf-8">
              <meta name="viewport" content="width=device-width, initial-scale=1">
              <title>正在验证访问</title>
              <style>body { margin: 0; min-height: 100vh; display: grid; place-items: center; background: #f8fafc; color: #0f172a; font: 16px system-ui, sans-serif; } main { width: min(92vw, 30rem); padding: 2rem; border: 1px solid #e2e8f0; border-radius: 1rem; background: white; box-shadow: 0 15px 45px rgba(15, 23, 42, .12); } h1 { margin-top: 0; font-size: 1.35rem; } p { color: #475569; line-height: 1.6; } #status { font-size: .875rem; color: #64748b; }</style>
            </head>
            <body>
              <main id="pow" data-challenge="__CHALLENGE__" data-difficulty="__DIFFICULTY__">
                <h1>正在进行安全验证</h1>
                <p>浏览器正在完成一次计算验证，通常只需几秒，完成后自动返回原页面。</p>
                <p id="status">验证中…</p>
                <noscript><p>请启用 JavaScript 后刷新页面。</p></noscript>
                <form id="verify-form" method="post" action="__VERIFY_PATH__" hidden>
                  <input type="hidden" name="state" value="__STATE__">
                  <input type="hidden" name="token" id="token">
                </form>
              </main>
              <script type="text/plain" id="pow-worker">__WORKER__</script>
              <script nonce="__NONCE__">
                (() => {
                  const root = document.getElementById('pow');
                  const form = document.getElementById('verify-form');
                  const status = document.getElementById('status');
                  const source = document.getElementById('pow-worker').textContent;
                  const worker = new Worker(URL.createObjectURL(new Blob([source], { type: 'text/javascript' })));
                  worker.onmessage = (event) => {
                    if (event.data.solution !== undefined) {
                      document.getElementById('token').value = event.data.solution;
                      status.textContent = '验证完成，正在返回…';
                      form.submit();
                    } else if (event.data.error) {
                      status.textContent = '验证失败，请刷新页面重试';
                    } else if (event.data.attempts) {
                      status.textContent = '验证中…已计算 ' + event.data.attempts.toLocaleString() + ' 次';
                    }
                  };
                  worker.postMessage({ challenge: root.dataset.challenge, difficulty: Number(root.dataset.difficulty) });
                })();
              </script>
            </body>
            </html>
            """;

        html = html
            .Replace("__CHALLENGE__", WebUtility.HtmlEncode(challenge), StringComparison.Ordinal)
            .Replace("__DIFFICULTY__", difficulty.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("__VERIFY_PATH__", WebUtility.HtmlEncode(RiskGuardMiddleware.VerifyPath), StringComparison.Ordinal)
            .Replace("__STATE__", WebUtility.HtmlEncode(state), StringComparison.Ordinal)
            .Replace("__WORKER__", ProofOfWork.WorkerScript, StringComparison.Ordinal)
            .Replace("__NONCE__", nonce, StringComparison.Ordinal);

        await context.Response.WriteAsync(html);
    }

    private static BotChallengeResponse InvalidChallenge()
    {
        return new BotChallengeResponse
        {
            Success = false,
            Message = "验证已失效，请刷新页面重试",
            ErrorCode = "risk_challenge_invalid"
        };
    }

    private static BotChallengeResponse Unavailable()
    {
        return new BotChallengeResponse
        {
            Success = false,
            Message = "人机验证服务暂时不可用",
            ErrorCode = "bot_protection_unavailable"
        };
    }
}
