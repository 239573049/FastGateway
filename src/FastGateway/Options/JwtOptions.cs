using System.Text;
using FastGateway.Infrastructure;

namespace FastGateway.Options;

public class JwtOptions
{
    public const string Name = "Jwt";

    /// <summary>
    ///     密钥
    /// </summary>
    public string Secret { get; set; }

    /// <summary>
    ///     过期时间 单位天
    /// </summary>
    public int ExpireDay { get; set; }

    /// <summary>
    ///     历史版本随仓库发布的密钥，公开可见，视同未配置
    /// </summary>
    private const string LegacyDefaultSecret =
        "FastGateway:(*123712(&(*$%^$))&(&(BIasd!@@#$$HGIK|\\|UGI))_+!@#$VBJasd:";

    /// <summary>
    ///     未配置、过短或仍为历史默认值时，改用 data/jwt-secret 中按安装随机生成的密钥
    /// </summary>
    public void ResolveSecret()
    {
        if (string.IsNullOrEmpty(Secret) || Secret == LegacyDefaultSecret || Encoding.UTF8.GetByteCount(Secret) < 32)
            Secret = LocalSecretStore.GetOrCreate("jwt-secret", 64).Value;

        if (ExpireDay <= 0) ExpireDay = 30;
    }
}