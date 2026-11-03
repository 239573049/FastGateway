using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Core.Entities;
using FastGateway.Infrastructure;
using FastGateway.Services;

namespace FastGateway.Risk.Model;

public enum RiskModelMode
{
    /// <summary>只记录模型判断，不影响决策</summary>
    Shadow = 0,

    /// <summary>模型判断写入风险标记，参与打分</summary>
    Enforce = 1
}

/// <summary>
///     AI 风控模型提供商配置（TypeSafe Jev），存于受保护设置 <see cref="RiskModelProviderStore.SettingKey" />
/// </summary>
public sealed partial class RiskModelProviderSettings
{
    public const string DefaultEndpoint = "https://api.typesafe.ai";
    public const string DefaultModel = "jev-latest";

    public bool Enabled { get; set; }

    /// <summary>预留多提供商，当前只实现 typesafe</summary>
    public string Provider { get; set; } = "typesafe";

    /// <summary>服务根地址，不含 /v1</summary>
    public string Endpoint { get; set; } = DefaultEndpoint;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = DefaultModel;

    public RiskModelMode Mode { get; set; } = RiskModelMode.Shadow;

    public int TimeoutMs { get; set; } = 3000;

    public int MaxRequestsPerSecond { get; set; } = 5;

    public int MaxConcurrency { get; set; } = 4;

    /// <summary>每日输入 token 上限，0 表示不限</summary>
    public long DailyTokenBudget { get; set; } = 5_000_000;

    /// <summary>是否启用 Jev HTTP 恶意请求定时识别。手动判断不受此开关限制。</summary>
    public bool HttpThreatEnabled { get; set; }

    /// <summary>Jev 恶意请求概率达到该值时自动封禁，默认 0.8。</summary>
    public double HttpThreatThreshold { get; set; } = 0.8;

    /// <summary>自动判断周期，默认 4 小时。</summary>
    public int HttpThreatIntervalHours { get; set; } = 4;

    /// <summary>聚合新请求时回看的小时数。</summary>
    public int HttpThreatLookbackHours { get; set; } = 24;

    /// <summary>单轮最多评估的 IP 数，避免一次消耗过多模型额度。</summary>
    public int HttpThreatMaxIpsPerRun { get; set; } = 20;

    /// <summary>观察模式：只记录判断结果，不封禁。</summary>
    public bool HttpThreatShadow { get; set; }

    /// <summary>首次封禁时长（小时），再犯翻倍；0 表示永久封禁。</summary>
    public int HttpThreatBanHours { get; set; } = 24;

    /// <summary>递增封禁时长上限（小时）。</summary>
    public int HttpThreatBanMaxHours { get; set; } = 720;

    /// <summary>手动解封 Jev 封禁的 IP 后，在该天数内不再交给 Jev 判断；0 表示不豁免。</summary>
    public int HttpThreatExemptDays { get; set; } = 7;

    [JsonIgnore]
    public bool IsUsable => Enabled && !string.IsNullOrEmpty(ApiKey);

    public RiskModelProviderSettings Clone()
    {
        return (RiskModelProviderSettings)MemberwiseClone();
    }

    /// <summary>
    ///     校验并规范化；非法时抛出 <see cref="ValidationException" />
    /// </summary>
    public void Normalize()
    {
        if (Provider != "typesafe") throw new ValidationException("当前只支持 TypeSafe 提供商");

        Endpoint = NormalizeEndpoint(Endpoint);
        if (!Uri.TryCreate(Endpoint, UriKind.Absolute, out var uri) ||
            !(uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp && IsLoopback(uri)) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ValidationException("服务地址必须是 https 地址（http 仅允许本机地址，用于调试）");

        Model = Model?.Trim() ?? string.Empty;
        if (!ModelPattern().IsMatch(Model)) throw new ValidationException("模型名只能包含字母、数字、点、下划线和连字符");

        ApiKey = ApiKey?.Trim() ?? string.Empty;
        if (ApiKey.Length > 512 || ApiKey.Any(char.IsWhiteSpace)) throw new ValidationException("API Key 格式不正确");

        if (!Enum.IsDefined(Mode)) throw new ValidationException("不支持的运行模式");
        if (TimeoutMs is < 500 or > 10_000) throw new ValidationException("超时需在 500-10000 毫秒之间");
        if (MaxRequestsPerSecond is < 1 or > 100) throw new ValidationException("每秒请求上限需在 1-100 之间");
        if (MaxConcurrency is < 1 or > 32) throw new ValidationException("并发上限需在 1-32 之间");
        if (DailyTokenBudget < 0) throw new ValidationException("每日 token 预算不能为负数");
        if (HttpThreatThreshold is < 0.5 or > 1) throw new ValidationException("恶意请求阈值需在 0.5-1 之间");
        if (HttpThreatIntervalHours is < 1 or > 168) throw new ValidationException("自动判断周期需在 1-168 小时之间");
        if (HttpThreatLookbackHours is < 1 or > 168) throw new ValidationException("请求回看范围需在 1-168 小时之间");
        if (HttpThreatMaxIpsPerRun is < 1 or > 200) throw new ValidationException("单轮 IP 上限需在 1-200 之间");
        if (HttpThreatBanHours is < 0 or > 8760) throw new ValidationException("封禁时长需在 0-8760 小时之间（0 为永久）");
        if (HttpThreatBanMaxHours is < 1 or > 8760) throw new ValidationException("封禁时长上限需在 1-8760 小时之间");
        if (HttpThreatBanHours > 0 && HttpThreatBanMaxHours < HttpThreatBanHours)
            throw new ValidationException("封禁时长上限不能小于首次封禁时长");
        if (HttpThreatExemptDays is < 0 or > 365) throw new ValidationException("解封豁免天数需在 0-365 之间");
        if (Enabled && ApiKey.Length == 0) throw new ValidationException("启用前请先填写 API Key");
    }

    public static string NormalizeEndpoint(string? endpoint)
    {
        var value = endpoint?.Trim().TrimEnd('/') ?? string.Empty;
        // 容忍用户粘贴带 /v1 的地址，请求时统一拼接
        return value.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? value[..^3] : value;
    }

    /// <summary>
    ///     两个地址是否指向同一主机：换了主机就必须重新填写 Key，防止把已保存的 Key 发往别处
    /// </summary>
    public static bool IsSameHost(string a, string b)
    {
        return Uri.TryCreate(NormalizeEndpoint(a), UriKind.Absolute, out var left) &&
               Uri.TryCreate(NormalizeEndpoint(b), UriKind.Absolute, out var right) &&
               string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.IdnHost, right.IdnHost, StringComparison.OrdinalIgnoreCase) &&
               left.Port == right.Port;
    }

    private static bool IsLoopback(Uri uri)
    {
        return uri.IsLoopback || IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address);
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex ModelPattern();
}

/// <summary>
///     读取、合并与保存提供商配置；API Key 只写不读
/// </summary>
public static class RiskModelProviderStore
{
    public const string SettingKey = "risk_model_provider";

    private static ConfigurationService? _configService;
    private static RiskModelProviderSettings _current = new();

    public static RiskModelProviderSettings Current => Volatile.Read(ref _current);

    public static event Action? Changed;

    public static void Initialize(ConfigurationService configService)
    {
        _configService = configService;
        ConfigurationService.ConfigurationChanged += Load;
        Load();
    }

    /// <summary>
    ///     把界面提交的配置合并到已保存的配置上：Key 留空表示保留原值，ClearApiKey 表示清除
    /// </summary>
    public static RiskModelProviderSettings Merge(RiskModelProviderSettings existing, RiskModelProviderInput input)
    {
        var endpoint = RiskModelProviderSettings.NormalizeEndpoint(input.Endpoint);
        var newKey = input.ApiKey?.Trim();
        string apiKey;
        if (input.ClearApiKey)
            apiKey = string.Empty;
        else if (!string.IsNullOrEmpty(newKey))
            apiKey = newKey;
        else if (existing.ApiKey.Length > 0 && !RiskModelProviderSettings.IsSameHost(existing.Endpoint, endpoint))
            throw new ValidationException("修改服务地址的主机后需要重新填写 API Key");
        else
            apiKey = existing.ApiKey;

        var merged = new RiskModelProviderSettings
        {
            Enabled = input.Enabled,
            Provider = string.IsNullOrWhiteSpace(input.Provider) ? "typesafe" : input.Provider.Trim(),
            Endpoint = endpoint,
            ApiKey = apiKey,
            Model = input.Model,
            Mode = input.Mode,
            TimeoutMs = input.TimeoutMs,
            MaxRequestsPerSecond = input.MaxRequestsPerSecond,
            MaxConcurrency = input.MaxConcurrency,
            DailyTokenBudget = input.DailyTokenBudget,
            HttpThreatEnabled = input.HttpThreatEnabled,
            HttpThreatThreshold = input.HttpThreatThreshold,
            HttpThreatIntervalHours = input.HttpThreatIntervalHours,
            HttpThreatLookbackHours = input.HttpThreatLookbackHours,
            HttpThreatMaxIpsPerRun = input.HttpThreatMaxIpsPerRun,
            HttpThreatShadow = input.HttpThreatShadow,
            HttpThreatBanHours = input.HttpThreatBanHours,
            HttpThreatBanMaxHours = input.HttpThreatBanMaxHours,
            HttpThreatExemptDays = input.HttpThreatExemptDays
        };
        merged.Normalize();
        return merged;
    }

    public static void Save(ConfigurationService configService, RiskModelProviderSettings settings)
    {
        configService.AddOrUpdateSetting(new Setting
        {
            Key = SettingKey,
            Value = JsonSerializer.Serialize(settings, AppJsonContext.Default.RiskModelProviderSettings),
            Description = "AI 风控模型提供商（含 API Key，请勿泄露）",
            IsPublic = false,
            IsSystem = true,
            Group = "risk"
        });
        Apply(settings);
    }

    public static RiskModelProviderView ToView(RiskModelProviderSettings settings)
    {
        return new RiskModelProviderView
        {
            Enabled = settings.Enabled,
            Provider = settings.Provider,
            Endpoint = settings.Endpoint,
            Model = settings.Model,
            Mode = settings.Mode,
            TimeoutMs = settings.TimeoutMs,
            MaxRequestsPerSecond = settings.MaxRequestsPerSecond,
            MaxConcurrency = settings.MaxConcurrency,
            DailyTokenBudget = settings.DailyTokenBudget,
            HttpThreatEnabled = settings.HttpThreatEnabled,
            HttpThreatThreshold = settings.HttpThreatThreshold,
            HttpThreatIntervalHours = settings.HttpThreatIntervalHours,
            HttpThreatLookbackHours = settings.HttpThreatLookbackHours,
            HttpThreatMaxIpsPerRun = settings.HttpThreatMaxIpsPerRun,
            HttpThreatShadow = settings.HttpThreatShadow,
            HttpThreatBanHours = settings.HttpThreatBanHours,
            HttpThreatBanMaxHours = settings.HttpThreatBanMaxHours,
            HttpThreatExemptDays = settings.HttpThreatExemptDays,
            ApiKeyConfigured = settings.ApiKey.Length > 0,
            ApiKeyHint = settings.ApiKey.Length >= 8 ? "****" + settings.ApiKey[^4..] : settings.ApiKey.Length > 0 ? "****" : null
        };
    }

    private static void Load()
    {
        if (_configService == null) return;

        var json = _configService.GetSetting(SettingKey)?.Value;
        var settings = new RiskModelProviderSettings();
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                settings = JsonSerializer.Deserialize(json, AppJsonContext.Default.RiskModelProviderSettings) ??
                           settings;
                settings.Normalize();
            }
            catch (Exception e) when (e is JsonException or ValidationException)
            {
                // 配置被手工改坏时停用模型，规则照常工作
                settings = new RiskModelProviderSettings();
            }
        }

        Apply(settings);
    }

    private static void Apply(RiskModelProviderSettings settings)
    {
        Volatile.Write(ref _current, settings);
        RiskEngine.Shared.ModelEnabled = settings.IsUsable;
        Changed?.Invoke();
    }
}

/// <summary>返回给界面的配置，不含 API Key</summary>
public sealed class RiskModelProviderView
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "typesafe";
    public string Endpoint { get; set; } = RiskModelProviderSettings.DefaultEndpoint;
    public string Model { get; set; } = RiskModelProviderSettings.DefaultModel;
    public RiskModelMode Mode { get; set; }
    public int TimeoutMs { get; set; }
    public int MaxRequestsPerSecond { get; set; }
    public int MaxConcurrency { get; set; }
    public long DailyTokenBudget { get; set; }
    public bool HttpThreatEnabled { get; set; }
    public double HttpThreatThreshold { get; set; }
    public int HttpThreatIntervalHours { get; set; }
    public int HttpThreatLookbackHours { get; set; }
    public int HttpThreatMaxIpsPerRun { get; set; }
    public bool HttpThreatShadow { get; set; }
    public int HttpThreatBanHours { get; set; }
    public int HttpThreatBanMaxHours { get; set; }
    public int HttpThreatExemptDays { get; set; }
    public bool ApiKeyConfigured { get; set; }
    public string? ApiKeyHint { get; set; }
}

/// <summary>界面提交的配置：ApiKey 为空表示保留已保存的 Key</summary>
public sealed class RiskModelProviderInput
{
    public bool Enabled { get; set; }
    public string? Provider { get; set; }
    public string Endpoint { get; set; } = RiskModelProviderSettings.DefaultEndpoint;
    public string? ApiKey { get; set; }
    public bool ClearApiKey { get; set; }
    public string Model { get; set; } = RiskModelProviderSettings.DefaultModel;
    public RiskModelMode Mode { get; set; }
    public int TimeoutMs { get; set; } = 3000;
    public int MaxRequestsPerSecond { get; set; } = 5;
    public int MaxConcurrency { get; set; } = 4;
    public long DailyTokenBudget { get; set; } = 5_000_000;
    public bool HttpThreatEnabled { get; set; }
    public double HttpThreatThreshold { get; set; } = 0.8;
    public int HttpThreatIntervalHours { get; set; } = 4;
    public int HttpThreatLookbackHours { get; set; } = 24;
    public int HttpThreatMaxIpsPerRun { get; set; } = 20;
    public bool HttpThreatShadow { get; set; }
    public int HttpThreatBanHours { get; set; } = 24;
    public int HttpThreatBanMaxHours { get; set; } = 720;
    public int HttpThreatExemptDays { get; set; } = 7;
}
