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
}