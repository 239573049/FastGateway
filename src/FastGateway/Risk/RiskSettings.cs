using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using Core.Entities;
using FastGateway.Infrastructure;
using FastGateway.Services;

namespace FastGateway.Risk;

/// <summary>
///     登录风控的可调参数，以 JSON 存在设置项 <see cref="RiskSettingsStore.SettingKey" /> 中
/// </summary>
public sealed class RiskSettings
{
    public const string ProviderPow = "pow";
    public const string ProviderTurnstile = "turnstile";

    public static RiskSettings Default { get; } = new();

    public int ChallengeThreshold { get; set; } = 30;

    public int StrongChallengeThreshold { get; set; } = 60;

    public int BlockThreshold { get; set; } = 90;

    /// <summary>中风险使用的验证方式</summary>
    public string MediumProvider { get; set; } = ProviderPow;

    /// <summary>高风险使用的验证方式；Turnstile 不可用时回落为加难的 PoW</summary>
    public string StrongProvider { get; set; } = ProviderTurnstile;

    /// <summary>PoW 前导零位数，强验证在此基础上 +3</summary>
    public int PowDifficulty { get; set; } = 18;

    /// <summary>一次验证换取的登录尝试次数</summary>
    public int ClearanceQuota { get; set; } = 5;

    public int ClearanceMinutes { get; set; } = 10;

    /// <summary>登录失败渐进延迟</summary>
    public bool FailureDelay { get; set; } = true;

    /// <summary>管理后台登录失败达到延迟阈值后，每次登录都要先完成 PoW</summary>
    public bool AdminLoginChallenge { get; set; } = true;

    /// <summary>管理后台每次登录都要先完成滑块验证</summary>
    public bool AdminLoginSlider { get; set; } = true;

    [JsonIgnore]
    public int StrongPowDifficulty => Math.Min(PowDifficulty + 3, ProofOfWork.MaxDifficulty);

    public void Validate()
    {
        if (ChallengeThreshold < 1 || StrongChallengeThreshold <= ChallengeThreshold ||
            BlockThreshold <= StrongChallengeThreshold || BlockThreshold > 1000)
            throw new ValidationException("阈值需满足 0 < 要求验证 < 强验证 < 封禁 ≤ 1000");

        if (!IsProvider(MediumProvider) || !IsProvider(StrongProvider))
            throw new ValidationException("验证方式仅支持 pow、turnstile");

        if (PowDifficulty is < ProofOfWork.MinDifficulty or > ProofOfWork.MaxDifficulty - 3)
            throw new ValidationException(
                $"PoW 难度需在 {ProofOfWork.MinDifficulty}-{ProofOfWork.MaxDifficulty - 3} 之间");

        if (ClearanceQuota is < 1 or > 50) throw new ValidationException("一次验证的登录次数需在 1-50 之间");
        if (ClearanceMinutes is < 1 or > 120) throw new ValidationException("验证有效期需在 1-120 分钟之间");
    }

    private static bool IsProvider(string? value)
    {
        return value is ProviderPow or ProviderTurnstile;
    }
}

/// <summary>
///     从系统设置加载风控参数并在配置变更（包括集群从节点应用快照）后刷新
/// </summary>
public static class RiskSettingsStore
{
    public const string SettingKey = "risk_settings";

    private static ConfigurationService? _configService;

    public static void Initialize(ConfigurationService configService)
    {
        _configService = configService;
        ConfigurationService.ConfigurationChanged += Load;
        Load();
    }

    public static void Save(ConfigurationService configService, RiskSettings settings)
    {
        settings.Validate();
        configService.AddOrUpdateSetting(new Setting
        {
            Key = SettingKey,
            Value = JsonSerializer.Serialize(settings, AppJsonContext.Default.RiskSettings),
            Description = "登录风控参数",
            IsPublic = false,
            IsSystem = true,
            Group = "risk"
        });
        RiskEngine.Shared.UpdateSettings(settings);
    }

    public static RiskSettings Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new RiskSettings();

        try
        {
            var settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.RiskSettings);
            settings?.Validate();
            return settings ?? new RiskSettings();
        }
        catch (Exception e) when (e is JsonException or ValidationException)
        {
            // 手工改坏配置文件时回落默认值，不让风控整体失效
            return new RiskSettings();
        }
    }

    private static void Load()
    {
        if (_configService == null) return;
        RiskEngine.Shared.UpdateSettings(Parse(_configService.GetSetting(SettingKey)?.Value));
    }
}
