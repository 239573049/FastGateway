using FastGateway.Infrastructure;

namespace FastGateway.Options;

public class FastGatewayOptions
{
    public const int DefaultMaxConnectionsPerUpstream = 4096;

    public const string DefaultPassword = "Aa123456";

    /// <summary>
    ///     历史版本内置的 TunnelToken，公开可见，视同未配置
    /// </summary>
    public const string LegacyDefaultTunnelToken = "Aa123456.";

    public const string GeneratedPasswordFileName = "admin-password";

    /// <summary>
    ///     全局 TunnelToken；未配置（或仍为历史默认值）时为 null，此时只接受节点级密钥
    /// </summary>
    public static string? TunnelToken { get; set; }

    public static string Password { get; set; } = DefaultPassword;

    /// <summary>
    ///     未配置密码时本次启动新生成了随机初始密码（需在日志中提示管理员）
    /// </summary>
    public static bool PasswordGenerated { get; private set; }

    public static bool IsDefaultPassword => Password == DefaultPassword;

    public static int MaxConnectionsPerUpstream { get; private set; } = DefaultMaxConnectionsPerUpstream;

    public static void Initialize(IConfiguration configuration)
    {
        var maxConnectionsValue = configuration["MaxConnectionsPerUpstream"]
                                  ?? configuration["MAX_CONNECTIONS_PER_UPSTREAM"];
        var maxConnections = DefaultMaxConnectionsPerUpstream;
        if (!string.IsNullOrWhiteSpace(maxConnectionsValue) &&
            (!int.TryParse(maxConnectionsValue, out maxConnections) || maxConnections is < 1 or > 65535))
            throw new ArgumentOutOfRangeException(nameof(MaxConnectionsPerUpstream),
                "MaxConnectionsPerUpstream must be between 1 and 65535.");

        MaxConnectionsPerUpstream = maxConnections;

        var tunnelToken = configuration["TunnelToken"];
        TunnelToken = string.IsNullOrEmpty(tunnelToken) || tunnelToken == LegacyDefaultTunnelToken
            ? null
            : tunnelToken;

        var password = configuration["Password"];
        if (string.IsNullOrEmpty(password)) password = configuration["PASSWORD"];

        if (!string.IsNullOrEmpty(password))
        {
            Password = password;
        }
        else
        {
            // 未配置时不再回退到公开的默认密码，而是生成随机初始密码写入 data/admin-password
            (Password, PasswordGenerated) = LocalSecretStore.GetOrCreate(GeneratedPasswordFileName, 16);
        }
    }
}