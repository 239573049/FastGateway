namespace FastGateway.Options;

public class JwtOptions
{
    /// <summary>
    ///     密钥
    /// </summary>
    public static string Secret { get; set; }

    /// <summary>
    ///     过期时间（天）
    /// </summary>
    public static int ExpireDay { get; set; } = 30;
}