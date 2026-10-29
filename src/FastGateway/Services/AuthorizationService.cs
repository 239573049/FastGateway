using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Options;
using FastGateway.Risk;

namespace FastGateway.Services;

public static class AuthorizationServiceExtensions
{
    public static IEndpointRouteBuilder MapAuthorizationService(this IEndpointRouteBuilder app)
    {
        var routeGroupBuilder = app.MapGroup("/api/v1/authorization")
            .WithTags("授权")
            .WithDescription("授权")
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("授权");

        routeGroupBuilder.MapGet("challenge-config", (BotProtectionService botProtection) =>
        {
            var config = botProtection.GetAdminChallengeConfig();
            config.SliderEnabled = RiskEngine.Shared.Settings.AdminLoginSlider;
            return config;
        });

        routeGroupBuilder.MapGet("slider",
            async (HttpContext context, SettingProvide settingProvide, AdminLoginGuard loginGuard) =>
        {
            var clientIp = await GetClientIpAsync(context, settingProvide);

            // 锁定期内不再发图：验证通过也登录不了，只会白白消耗渲染
            if (loginGuard.GetLockRemaining(clientIp) is { } remaining)
                throw TooManyFailures(context, remaining);

            var challenge = SliderCaptcha.Shared.Create(clientIp)
                            ?? throw new ValidationException("验证请求过于频繁，请稍后重试");

            context.Response.Headers.CacheControl = "no-store";
            return new SliderChallengeDto
            {
                Id = challenge.Id,
                Background = "data:image/png;base64," + Convert.ToBase64String(challenge.Background),
                Piece = "data:image/png;base64," + Convert.ToBase64String(challenge.Piece),
                Width = SliderCaptcha.Width,
                Height = SliderCaptcha.Height,
                PieceSize = SliderCaptcha.PieceSize
            };
        });

        routeGroupBuilder.MapPost("slider",
            async (HttpContext context, SettingProvide settingProvide, AdminLoginGuard loginGuard,
                SliderVerifyRequest request) =>
            {
                var clientIp = await GetClientIpAsync(context, settingProvide);
                if (loginGuard.GetLockRemaining(clientIp) is { } remaining)
                    throw TooManyFailures(context, remaining);

                var ticket = SliderCaptcha.Shared.Verify(request.Id, request.X, request.Track, clientIp);
                if (ticket != null) return ticket;

                // 滑块失败与密码失败共用计数：脚本反复试滑块同样会被锁定；
                // 阈值远高于正常人手滑的次数，不会误伤
                if (loginGuard.RecordFailure(clientIp).LockedFor is { } lockedFor)
                    throw TooManyFailures(context, lockedFor);

                throw new ValidationException("验证未通过，请重试");
            });

        routeGroupBuilder.MapGet("security-status", () => new AdminSecurityStatusDto
            {
                UsingDefaultPassword = FastGatewayOptions.IsDefaultPassword
            })
            .RequireAuthorization();

        // PoW 求解器脚本与网关验证页共用同一份实现
        routeGroupBuilder.MapGet("pow-worker.js",
            () => Results.Text(ProofOfWork.WorkerScript, "text/javascript; charset=utf-8"));

        routeGroupBuilder.MapPost(string.Empty,
            async Task<object> (HttpContext context, JwtHelper jwtHelper, BotProtectionService botProtection,
                AdminLoginGuard loginGuard, SettingProvide settingProvide,
                AuthorizationRequest request, CancellationToken cancellationToken) =>
            {
                var clientIp = await GetClientIpAsync(context, settingProvide);

                // 锁定期内即使密码正确也拒绝，否则锁定对爆破没有约束力；放在人机验证之前，避免被锁来源继续消耗验证调用
                if (loginGuard.GetLockRemaining(clientIp) is { } remaining)
                    throw TooManyFailures(context, remaining);

                // 失败达到延迟阈值后，每次尝试都要先完成一次新的 PoW，再校验密码
                var engine = RiskEngine.Shared;
                var recentFailures = loginGuard.GetRecentFailures(clientIp);
                var host = context.Request.Host.Host;
                if (engine.Settings.AdminLoginChallenge && engine.HasSigningKey &&
                    recentFailures >= AdminLoginGuard.DelayThreshold &&
                    !engine.TryRedeemPow(request.RiskState, request.RiskSolution, host, clientIp, AdminScope))
                {
                    var difficulty = recentFailures >= AdminLoginGuard.DelayThreshold * 2
                        ? engine.Settings.StrongPowDifficulty
                        : engine.Settings.PowDifficulty;
                    var state = engine.CreateChallengeState(host, AdminScope, "/", clientIp,
                        RiskSettings.ProviderPow, difficulty)!;
                    engine.TryReadChallengeState(state, host, clientIp, out var payload);
                    return ResultDto.CreateFailed("请先完成安全验证", new RiskPowChallengeDto
                    {
                        State = state,
                        Challenge = payload.Nonce,
                        Difficulty = difficulty
                    });
                }

                // 放在 PoW 之后：PoW 挑战响应会让客户端带同一票据重试，此前不能把票据消费掉
                if (engine.Settings.AdminLoginSlider &&
                    !SliderCaptcha.Shared.RedeemTicket(request.SliderTicket, clientIp))
                    throw new ValidationException("请先完成滑块验证");

                if (botProtection.AdminLoginEnabled)
                {
                    var verification = await botProtection.VerifyTurnstileAsync(
                        request.TurnstileToken,
                        "admin-login",
                        context.Request.Host.Host,
                        clientIp,
                        cancellationToken);

                    if (verification.Status == BotVerificationStatus.Unavailable)
                        throw new ValidationException("人机验证服务暂时不可用，请稍后重试");

                    if (verification.Status != BotVerificationStatus.Success)
                        throw new ValidationException("请先完成人机验证");
                }

                if (AdminLoginGuard.PasswordMatches(request.Password, FastGatewayOptions.Password))
                {
                    loginGuard.RecordSuccess(clientIp);
                    return jwtHelper.CreateToken();
                }

                var failure = loginGuard.RecordFailure(clientIp);
                await loginGuard.DelayAsync(failure.Delay, cancellationToken);

                if (failure.LockedFor is { } lockedFor)
                    throw TooManyFailures(context, lockedFor);

                throw new ValidationException("密码错误");
            });

        return app;
    }

    /// <summary>管理后台登录在风控挑战状态中的 RouteId，与业务路由区分</summary>
    private const string AdminScope = "admin";

    /// <summary>滑块挑战与登录必须用同一 IP 来源，否则票据绑定的 IP 对不上</summary>
    private static async Task<string> GetClientIpAsync(HttpContext context, SettingProvide settingProvide)
    {
        var ipSource = await settingProvide.GetEnumAsync<ClientIpSource>(AdminLoginGuard.ClientIpSourceSettingKey);
        return ClientIpHelper.GetClientIp(context, ipSource);
    }

    /// <summary>
    ///     由 <see cref="ResultFilter" /> 包装为失败结果，响应保持此处设置的 429 状态码
    /// </summary>
    private static ValidationException TooManyFailures(HttpContext context, TimeSpan remaining)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter =
            ((int)Math.Ceiling(remaining.TotalSeconds)).ToString(CultureInfo.InvariantCulture);

        // 升级锁定最长 24 小时，按小时提示更易读
        var wait = remaining >= TimeSpan.FromHours(1)
            ? $"{(int)Math.Ceiling(remaining.TotalHours)} 小时"
            : $"{(int)Math.Ceiling(remaining.TotalMinutes)} 分钟";
        return new ValidationException($"登录失败次数过多，请 {wait}后重试");
    }
}