using System.Security.Cryptography;
using Core.Entities;
using FastGateway.Cluster;
using FastGateway.Services;

namespace FastGateway.Risk;

/// <summary>
///     风控签名密钥（clearance、验证状态、账号哈希）。首次需要时由主网关或独立网关自动生成并写入设置，
///     随配置快照同步到从节点，保证集群内各节点签发的 clearance 互认。
/// </summary>
public static class RiskSigningKey
{
    public const string SettingKey = "risk_signing_key";

    private static ConfigurationService? _configService;
    private static ClusterStateService? _clusterState;
    private static int _loading;

    /// <summary>
    ///     含机密的设置键：通用设置接口既不返回也不允许写入
    /// </summary>
    public static bool IsProtectedSetting(string? key)
    {
        return string.Equals(key, SettingKey, StringComparison.Ordinal) ||
               string.Equals(key, Model.RiskModelProviderStore.SettingKey, StringComparison.Ordinal);
    }

    /// <summary>
    ///     由专用接口校验后写入的设置键：通用设置接口可以读取，但不允许绕过校验直接写入
    /// </summary>
    public static bool IsManagedSetting(string? key)
    {
        return IsProtectedSetting(key) || string.Equals(key, RiskSettingsStore.SettingKey, StringComparison.Ordinal);
    }

    public static void Initialize(ConfigurationService configService, ClusterStateService clusterState)
    {
        _configService = configService;
        _clusterState = clusterState;
        ConfigurationService.ConfigurationChanged += Load;
        Load();
    }

    private static void Load()
    {
        var configService = _configService;
        if (configService == null) return;

        // 生成密钥会写配置并再次触发 ConfigurationChanged，避免重入
        if (Interlocked.Exchange(ref _loading, 1) == 1) return;
        try
        {
            if (TryDecode(configService.GetSetting(SettingKey)?.Value, out var key))
            {
                RiskEngine.Shared.SetSigningKey(key);
                return;
            }

            // 从节点等待主网关下发，自行生成会与主网关不一致
            if (_clusterState?.Get().Role == ClusterRole.Worker) return;

            key = RandomNumberGenerator.GetBytes(32);
            configService.AddOrUpdateSetting(new Setting
            {
                Key = SettingKey,
                Value = Convert.ToBase64String(key),
                Description = "登录风控签名密钥（自动生成，请勿泄露）",
                IsPublic = false,
                IsSystem = true,
                Group = "risk"
            });
            RiskEngine.Shared.SetSigningKey(key);
        }
        finally
        {
            Interlocked.Exchange(ref _loading, 0);
        }
    }

    private static bool TryDecode(string? value, out byte[] key)
    {
        key = [];
        if (string.IsNullOrWhiteSpace(value)) return false;

        try
        {
            key = Convert.FromBase64String(value);
            return key.Length >= 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
