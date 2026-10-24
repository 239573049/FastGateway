namespace FastGateway.Risk;

/// <summary>
///     环形分桶的滑动窗口计数：窗口被均分为若干桶，过期的桶在复用时清零，内存固定
/// </summary>
public sealed class SlidingWindowCounter
{
    private readonly Lock _sync = new();
    private readonly long _bucketTicks;
    private readonly long[] _bucketKeys;
    private readonly int[] _bucketCounts;

    public SlidingWindowCounter(TimeSpan window, int bucketCount)
    {
        _bucketTicks = window.Ticks / bucketCount;
        _bucketKeys = new long[bucketCount];
        _bucketCounts = new int[bucketCount];
    }

    public int Add(long nowTicks)
    {
        var key = nowTicks / _bucketTicks;
        var slot = (int)(key % _bucketKeys.Length);

        lock (_sync)
        {
            if (_bucketKeys[slot] != key)
            {
                _bucketKeys[slot] = key;
                _bucketCounts[slot] = 0;
            }

            _bucketCounts[slot]++;
            return SumUnsafe(key);
        }
    }

    public int Count(long nowTicks)
    {
        lock (_sync)
        {
            return SumUnsafe(nowTicks / _bucketTicks);
        }
    }

    private int SumUnsafe(long currentKey)
    {
        var oldestKey = currentKey - _bucketKeys.Length + 1;
        var total = 0;
        for (var i = 0; i < _bucketKeys.Length; i++)
        {
            var key = _bucketKeys[i];
            if (key >= oldestKey && key <= currentKey) total += _bucketCounts[i];
        }

        return total;
    }
}

/// <summary>
///     窗口内的去重计数（如某 IP 尝试过的账号数）。最多记录 capacity 个值，
///     判定阈值远小于容量，满员后不再接收新值，计数停在容量上
/// </summary>
public sealed class DistinctWindowSet
{
    private readonly Lock _sync = new();
    private readonly int _capacity;
    private readonly long _windowTicks;
    private readonly Dictionary<string, long> _lastSeen = new(StringComparer.Ordinal);

    public DistinctWindowSet(TimeSpan window, int capacity)
    {
        _windowTicks = window.Ticks;
        _capacity = capacity;
    }

    public int Add(string value, long nowTicks)
    {
        lock (_sync)
        {
            PruneUnsafe(nowTicks);
            if (_lastSeen.Count < _capacity || _lastSeen.ContainsKey(value)) _lastSeen[value] = nowTicks;
            return _lastSeen.Count;
        }
    }

    public int Count(long nowTicks)
    {
        lock (_sync)
        {
            PruneUnsafe(nowTicks);
            return _lastSeen.Count;
        }
    }

    private void PruneUnsafe(long nowTicks)
    {
        List<string>? expired = null;
        foreach (var (value, seen) in _lastSeen)
        {
            if (nowTicks - seen >= _windowTicks) (expired ??= []).Add(value);
        }

        if (expired == null) return;
        foreach (var value in expired) _lastSeen.Remove(value);
    }
}
