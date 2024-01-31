namespace Gateway.Middlewares.FlowAnalytics;

public sealed class FlowAnalyzer : IFlowAnalyzer
{
    private const int INTERVAL_SECONDS = 5;
    private readonly FlowQueues _readQueues = new(INTERVAL_SECONDS);
    private readonly FlowQueues _writeQueues = new(INTERVAL_SECONDS);

    /// <summary>
    /// 收到数据
    /// </summary>
    /// <param name="flowType"></param>
    /// <param name="length"></param>
    public void OnFlow(FlowType flowType, int length)
    {
        if (flowType == FlowType.Read)
        {
            _readQueues.OnFlow(length);
        }
        else
        {
            _writeQueues.OnFlow(length);
        }
    }

    public void CleanRecords()
    {
        _readQueues.CleanRecords();
        _writeQueues.CleanRecords();
    }

    /// <summary>
    /// 获取流量分析
    /// </summary>
    /// <returns></returns>
    public FlowStatisticsDto GetFlowStatistics()
    {
        return new FlowStatisticsDto
        {
            TotalRead = _readQueues.TotalBytes,
            TotalWrite = _writeQueues.TotalBytes,
            ReadRate = _readQueues.GetRate(),
            WriteRate = _writeQueues.GetRate()
        };
    }

    private class FlowQueues(int intervalSeconds)
    {
        private int _cleaning = 0;
        private long _totalBytes = 0L;

        private record QueueItem(long Ticks, int Length);

        private readonly ConcurrentQueue<QueueItem> _queues = new();

        public long TotalBytes => this._totalBytes;

        public void OnFlow(int length)
        {
            Interlocked.Add(ref this._totalBytes, length);
            this.CleanInvalidRecords();
            this._queues.Enqueue(new QueueItem(Environment.TickCount64, length));
        }

        public double GetRate()
        {
            CleanInvalidRecords();
            return (double)_queues.Sum(item => item.Length) / intervalSeconds;
        }

        /// <summary>
        /// 清除无效记录
        /// </summary>
        /// <returns></returns>
        private bool CleanInvalidRecords()
        {
            if (Interlocked.CompareExchange(ref this._cleaning, 1, 0) != 0)
            {
                return false;
            }

            var ticks = Environment.TickCount64;
            while (_queues.TryPeek(out var item))
            {
                if (ticks - item.Ticks < intervalSeconds * 1000)
                {
                    break;
                }

                _queues.TryDequeue(out _);
            }

            Interlocked.Exchange(ref _cleaning, 0);
            return true;
        }


        public void CleanRecords()
        {
            _queues.Clear();
            Interlocked.Exchange(ref _cleaning, 0);
        }
    }
}