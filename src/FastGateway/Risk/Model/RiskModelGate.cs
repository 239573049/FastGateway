namespace FastGateway.Risk.Model;

/// <summary>
///     模型调用的准入控制：每秒上限、并发上限、每日 token 预算、提供商限流退避与连续失败熔断。
///     任何一项不满足就跳过本次评估，风控回落到规则，不排队等待
/// </summary>
public sealed class RiskModelGate(TimeProvider timeProvider)
{
    public const int CircuitFailureThreshold = 5;
    public static readonly TimeSpan CircuitOpenDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan DefaultRateLimitBackoff = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan MaxRetryAfter = TimeSpan.FromMinutes(5);

    private readonly Lock _sync = new();
    private long _secondKey;
    private int _secondCount;
    private int _inFlight;
    private DateOnly _tokenDay;
    private long _tokensUsed;
    private int _consecutiveFailures;
    private long _openUntil;
    private long _retryAfterUntil;

    private long Now => timeProvider.GetUtcNow().UtcTicks;

    public long TokensUsedToday
    {
        get
        {
            lock (_sync)
            {
                RollDay();
                return _tokensUsed;
            }
        }
    }

    public DateTimeOffset? CircuitOpenUntil => ToTime(Volatile.Read(ref _openUntil));

    public DateTimeOffset? BackoffUntil => ToTime(Volatile.Read(ref _retryAfterUntil));

    /// <summary>
    ///     尝试占用一次调用名额；成功后必须调用 <see cref="Release" />
    /// </summary>
    public bool TryAcquire(RiskModelProviderSettings settings, out string? reason)
    {
        var now = Now;
        lock (_sync)
        {
            RollDay();
            reason = now < _openUntil ? "连续失败熔断中"
                : now < _retryAfterUntil ? "提供商限流退避中"
                : settings.DailyTokenBudget > 0 && _tokensUsed >= settings.DailyTokenBudget ? "已达每日 token 预算"
                : _inFlight >= settings.MaxConcurrency ? "已达并发上限"
                : null;
            if (reason != null) return false;

            var second = now / TimeSpan.TicksPerSecond;
            if (second != _secondKey)
            {
                _secondKey = second;
                _secondCount = 0;
            }

            if (_secondCount >= settings.MaxRequestsPerSecond)
            {
                reason = "已达每秒请求上限";
                return false;
            }

            _secondCount++;
            _inFlight++;
            return true;
        }
    }

    public void Release()
    {
        lock (_sync)
        {
            _inFlight = Math.Max(0, _inFlight - 1);
        }
    }

    public void RecordSuccess(long inputTokens)
    {
        lock (_sync)
        {
            RollDay();
            _tokensUsed += Math.Max(0, inputTokens);
            _consecutiveFailures = 0;
        }
    }

    public void RecordFailure(JevResult result)
    {
        var now = Now;
        lock (_sync)
        {
            if (result.Error == JevErrorKind.RateLimited)
            {
                var wait = result.RetryAfter is { } retryAfter && retryAfter > TimeSpan.Zero
                    ? retryAfter < MaxRetryAfter ? retryAfter : MaxRetryAfter
                    : DefaultRateLimitBackoff;
                _retryAfterUntil = now + wait.Ticks;
            }

            if (++_consecutiveFailures < CircuitFailureThreshold) return;

            _openUntil = now + CircuitOpenDuration.Ticks;
            _consecutiveFailures = 0;
        }
    }

    private void RollDay()
    {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        if (today == _tokenDay) return;

        _tokenDay = today;
        _tokensUsed = 0;
    }

    private DateTimeOffset? ToTime(long ticks)
    {
        return ticks > Now ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;
    }
}

public static class RiskModelScoring
{
    public const int MaxModelScore = 40;

    /// <summary>
    ///     模型只回答原子问题，分数由这里组合；上限 40 分，单独最多把来源推到「要求验证」档
    /// </summary>
    public static int Combine(double automated, double credentialStuffing, double passwordGuessing)
    {
        var score = 25 * Clamp(automated) + 30 * Clamp(credentialStuffing) + 20 * Clamp(passwordGuessing);
        return (int)Math.Round(Math.Min(MaxModelScore, score));
    }

    private static double Clamp(double value)
    {
        return double.IsNaN(value) ? 0 : Math.Clamp(value, 0, 1);
    }
}
