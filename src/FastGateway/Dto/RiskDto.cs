namespace FastGateway.Dto;

public sealed class RiskTagDto
{
    public string Id { get; set; } = string.Empty;

    /// <summary>block（封禁）/ account_under_attack（账号被分布式爆破）/ admin_lock（管理后台登录锁定）</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>ip / subnet / account</summary>
    public string SubjectType { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    /// <summary>站点域名；管理后台锁定为空</summary>
    public string? Host { get; set; }

    public string Reason { get; set; } = string.Empty;

    public int Score { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }
}

public sealed class RiskDecisionDto
{
    public DateTime Time { get; set; }

    public string Ip { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    /// <summary>challenge / block</summary>
    public string Action { get; set; } = string.Empty;

    public int Score { get; set; }

    public string Reason { get; set; } = string.Empty;
}

public sealed class RiskChallengeState
{
    public string Host { get; set; } = string.Empty;

    public string RouteId { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = "/";

    public string Ip { get; set; } = string.Empty;

    /// <summary>pow / turnstile，签名保护，客户端无法降级</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>PoW 前导零位数；Turnstile 为 0。PoW 的挑战串即 <see cref="Nonce" /></summary>
    public int Difficulty { get; set; }

    public long IssuedAt { get; set; }

    public long ExpiresAt { get; set; }

    public string Nonce { get; set; } = string.Empty;
}

/// <summary>
///     返回给浏览器的 PoW 挑战
/// </summary>
public sealed class RiskPowChallengeDto
{
    public string State { get; set; } = string.Empty;

    public string Challenge { get; set; } = string.Empty;

    public int Difficulty { get; set; }
}

public sealed class RiskClearancePayload
{
    public string Host { get; set; } = string.Empty;

    public string RouteId { get; set; } = string.Empty;

    public string Ip { get; set; } = string.Empty;

    public long IssuedAt { get; set; }

    public long ExpiresAt { get; set; }

    public string Nonce { get; set; } = string.Empty;
}
