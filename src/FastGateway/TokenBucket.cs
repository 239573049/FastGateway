namespace FastGateway;

/// <summary>
///     令牌桶，用于限制流量。
/// </summary>
public struct TokenBucket(int maxTokens)
{
    private readonly int _maxTokens = maxTokens;
    private int _tokens = maxTokens;
    private DateTime _lastCheck = DateTime.UtcNow;

    private bool GetTokens(int count)
    {
        var now = DateTime.UtcNow;
        if ((now - _lastCheck).TotalSeconds > 1)
        {
            _tokens = _maxTokens;
            _lastCheck = now;
        }

        if (_tokens < count) return false;

        _tokens -= count;

        return true;
    }

    public async ValueTask WaitForTokens(int count)
    {
        while (!GetTokens(count))
        {
            var now = DateTime.UtcNow;
            var timeToNextToken = (now - _lastCheck).Microseconds;
            await Task.Delay(timeToNextToken);
        }
    }
}