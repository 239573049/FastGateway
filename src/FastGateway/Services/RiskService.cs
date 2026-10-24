using System.ComponentModel.DataAnnotations;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Risk;
using FastGateway.Risk.Model;

namespace FastGateway.Services;

public static class RiskService
{
    private const string AdminLockPrefix = "admin:";
    private const int MaxProviderCallsPerMinute = 10;

    // 测试连接与获取模型列表会消耗提供商额度，全局限速，防止误操作或脚本反复调用
    private static readonly SlidingWindowCounter ProviderCalls = new(TimeSpan.FromMinutes(1), 6);

    public static IEndpointRouteBuilder MapRisk(this IEndpointRouteBuilder app)
    {
        var risk = app.MapGroup("/api/v1/risk")
            .WithTags("安全防护")
            .WithDescription("登录风控")
            .RequireAuthorization()
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("登录风控");

        risk.MapGet("tags", (AdminLoginGuard adminLoginGuard) =>
            {
                var tags = RiskEngine.Shared.GetActiveTags().ToList();
                tags.AddRange(adminLoginGuard.GetActiveLocks().Select(x => new RiskTagDto
                {
                    Id = AdminLockPrefix + (x.IsSubnet ? "subnet:" : "ip:") + x.Subject,
                    Kind = "admin_lock",
                    SubjectType = x.IsSubnet ? "subnet" : "ip",
                    Subject = x.Subject,
                    Reason = x.IsSubnet
                        ? $"该网段 {AdminLoginGuard.Window.TotalMinutes} 分钟内管理后台登录失败 {AdminLoginGuard.SubnetLockThreshold} 次"
                        : $"{AdminLoginGuard.Window.TotalMinutes} 分钟内管理后台登录失败 {AdminLoginGuard.IpLockThreshold} 次",
                    CreatedAt = x.LockedAt.LocalDateTime,
                    ExpiresAt = x.LockedUntil.LocalDateTime
                }));

                return tags.OrderByDescending(x => x.CreatedAt).ToList();
            })
            .WithDescription("获取当前生效的风险标记")
            .WithDisplayName("获取风险标记");

        risk.MapDelete("tags", (AdminLoginGuard adminLoginGuard, string id) =>
            {
                var removed = id.StartsWith(AdminLockPrefix, StringComparison.Ordinal)
                    ? RemoveAdminLock(adminLoginGuard, id[AdminLockPrefix.Length..])
                    : RiskEngine.Shared.RemoveTag(id);

                if (!removed) throw new ValidationException("标记不存在或已过期");
            })
            .WithDescription("解除风险标记")
            .WithDisplayName("解除风险标记");

        risk.MapGet("decisions", () => RiskEngine.Shared.GetRecentDecisions())
            .WithDescription("获取最近的验证与封禁决策")
            .WithDisplayName("获取风控决策");

        risk.MapGet("settings", () => RiskEngine.Shared.Settings)
            .WithDescription("获取登录风控参数")
            .WithDisplayName("获取风控参数");

        risk.MapPut("settings", (ConfigurationService configService, RiskSettings settings) =>
                RiskSettingsStore.Save(configService, settings))
            .WithDescription("修改登录风控参数")
            .WithDisplayName("修改风控参数");

        risk.MapGet("model-provider", () => RiskModelProviderStore.ToView(RiskModelProviderStore.Current))
            .WithDescription("获取 AI 风控模型提供商配置（不含 API Key）")
            .WithDisplayName("获取模型提供商");

        risk.MapPut("model-provider", (ConfigurationService configService, RiskModelProviderInput input) =>
            {
                var settings = RiskModelProviderStore.Merge(RiskModelProviderStore.Current, input);
                RiskModelProviderStore.Save(configService, settings);
                return RiskModelProviderStore.ToView(settings);
            })
            .WithDescription("修改 AI 风控模型提供商配置")
            .WithDisplayName("修改模型提供商");

        risk.MapPost("model-provider/test",
                async (JevClient client, RiskModelProviderInput input, CancellationToken cancellationToken) =>
                {
                    var settings = PrepareCall(input);
                    var request = new JevRequest
                    {
                        Model = settings.Model,
                        State = new RiskFeatureState
                        {
                            LoginAttempts = 30, LoginFailures = 30, FailureRatio = 1, DistinctAccounts = 25,
                            MedianIntervalMs = 400, IntervalStddevMs = 10, ClientKind = ClientKinds.ScriptLibrary,
                            NetworkType = "hosting", RuleScore = 70
                        },
                        Questions = new Dictionary<string, JevQuestion>
                        {
                            [RiskModelScorerService.QuestionAutomated] = new()
                            {
                                Instructions = RiskModelScorerService.Questions[RiskModelScorerService.QuestionAutomated]
                            }
                        }
                    };

                    var result = await client.EvaluateAsync(settings, request, cancellationToken);
                    var answers = result.Response?.Answers;
                    return new RiskModelTestResult
                    {
                        Success = result.Success,
                        LatencyMs = result.LatencyMs,
                        Model = result.Response?.Model,
                        Automated = answers != null &&
                                    answers.TryGetValue(RiskModelScorerService.QuestionAutomated, out var answer)
                            ? answer.Noul
                            : null,
                        Message = result.Message
                    };
                })
            .WithDescription("用当前填写的配置测试连接（发送一条示例特征）")
            .WithDisplayName("测试模型提供商");

        risk.MapPost("model-provider/models",
                async (JevClient client, RiskModelProviderInput input, CancellationToken cancellationToken) =>
                {
                    var (result, models) = await client.ListModelsAsync(PrepareCall(input), cancellationToken);
                    if (result.Error != JevErrorKind.None)
                        throw new ValidationException(result.Message ?? "获取模型列表失败");

                    return models;
                })
            .WithDescription("获取提供商可用的模型列表")
            .WithDisplayName("获取模型列表");

        risk.MapGet("model-status", (RiskModelScorerService scorer) => scorer.GetStatus())
            .WithDescription("获取 AI 风控模型的运行状态")
            .WithDisplayName("获取模型状态");

        risk.MapGet("model-evaluations", (RiskModelScorerService scorer) => scorer.GetEvaluations())
            .WithDescription("获取最近的模型评估记录")
            .WithDisplayName("获取模型评估");

        return app;
    }

    /// <summary>
    ///     测试与获取模型列表使用界面上尚未保存的配置；Key 的保留规则与保存时一致（换主机必须重填）
    /// </summary>
    private static RiskModelProviderSettings PrepareCall(RiskModelProviderInput input)
    {
        if (ProviderCalls.Count(DateTimeOffset.UtcNow.UtcTicks) >= MaxProviderCallsPerMinute)
            throw new ValidationException("操作过于频繁，请稍后再试");

        input.Enabled = false;
        var settings = RiskModelProviderStore.Merge(RiskModelProviderStore.Current, input);
        if (settings.ApiKey.Length == 0) throw new ValidationException("请先填写 API Key");

        ProviderCalls.Add(DateTimeOffset.UtcNow.UtcTicks);
        return settings;
    }

    private static bool RemoveAdminLock(AdminLoginGuard adminLoginGuard, string value)
    {
        if (value.StartsWith("subnet:", StringComparison.Ordinal))
            return adminLoginGuard.Unlock(value["subnet:".Length..], isSubnet: true);

        return value.StartsWith("ip:", StringComparison.Ordinal) &&
               adminLoginGuard.Unlock(value["ip:".Length..], isSubnet: false);
    }
}
