using System.Text.Json.Serialization;

namespace FastGateway.Dto;

public sealed class AuthorizationRequest
{
    public string Password { get; set; } = string.Empty;

    public string? TurnstileToken { get; set; }

    /// <summary>登录失败过多时要求的 PoW：服务端下发的挑战状态</summary>
    public string? RiskState { get; set; }

    /// <summary>PoW 解</summary>
    public string? RiskSolution { get; set; }

    /// <summary>滑块验证通过后换取的一次性票据</summary>
    public string? SliderTicket { get; set; }
}

public sealed class BotChallengeConfigDto
{
    public bool Enabled { get; set; }

    public bool Configured { get; set; }

    public string SiteKey { get; set; } = string.Empty;

    /// <summary>管理后台登录是否要求滑块验证（仅后台登录使用）</summary>
    public bool SliderEnabled { get; set; }
}

public sealed class BotVerifyRequest
{
    public string Token { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;
}

public sealed class BotChallengeResponse
{
    public bool Success { get; set; }

    public string? RedirectUrl { get; set; }

    public string? Message { get; set; }

    public string? ErrorCode { get; set; }
}

public sealed class BotChallengeState
{
    public string Host { get; set; } = string.Empty;

    public string RouteId { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = string.Empty;

    public string RoutePath { get; set; } = "/";

    public string Action { get; set; } = string.Empty;

    public long IssuedAt { get; set; }

    public long ExpiresAt { get; set; }

    public string Nonce { get; set; } = string.Empty;
}

public sealed class BotClearancePayload
{
    public string Host { get; set; } = string.Empty;

    public string RouteId { get; set; } = string.Empty;

    public string RoutePath { get; set; } = "/";

    public long IssuedAt { get; set; }

    public long ExpiresAt { get; set; }

    public string? Ip { get; set; }

    public string Nonce { get; set; } = string.Empty;
}

public sealed class TurnstileVerifyResponse
{
    public bool Success { get; set; }

    public string? Action { get; set; }

    public string? Hostname { get; set; }

    [JsonPropertyName("error-codes")]
    public string[] ErrorCodes { get; set; } = [];
}
