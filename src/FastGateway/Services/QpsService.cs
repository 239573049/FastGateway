using System.Collections.Concurrent;

namespace FastGateway.Services;

public sealed class QpsItem
{
    private int _lastQps;
    public void IncrementRequests()
    {
        Interlocked.Increment(ref _lastQps);
    }

    public void CalculateQps()
    {
        Interlocked.Exchange(ref _lastQps, 0);
    }

    public int GetQps()
    {
        return _lastQps;
    }
}

public sealed class QpsService : IQpsService, IDisposable
{
    private readonly ConcurrentDictionary<string, QpsItem> _qps = new();
    private bool _enable;

    public void AddServiceQps(string serviceId)
    {
        // 判断是否存在
        if (!_qps.ContainsKey(serviceId))
        {
            _qps.TryAdd(serviceId, new QpsItem());
        }
    }

    public void IncrementServiceRequests(string serviceId)
    {
        if (_enable == false)
        {
            return;
        }

        if (_qps.TryGetValue(serviceId, out var qpsItem))
        {
            qpsItem.IncrementRequests();
        }
    }

    public void CalculateServiceQps(object? state)
    {
        if (state is not string serviceId || _enable == false)
        {
            return;
        }

        if (!string.IsNullOrEmpty(serviceId))
        {
            if (_qps.TryGetValue(serviceId, out var qpsItem))
            {
                qpsItem.CalculateQps();
            }
        }
        else
        {
            // 清空所有服务的QPS统计
            foreach (var qpsItem in _qps.Values)
            {
                qpsItem.CalculateQps();
            }
        }
    }

    public int GetServiceQps(string? serviceId)
    {
        if (!string.IsNullOrEmpty(serviceId))
        {
            if (!_qps.TryGetValue(serviceId, out var qpsItem)) return 0;
            
            var qps = qpsItem.GetQps();
                
            qpsItem.CalculateQps();
                
            return qps;
        }
        else
        {
            // 获取所有服务的QPS统计
            return _qps.Values.Sum(x =>
            {
                var qps = x.GetQps();
                x.CalculateQps();
                return qps;
            });
        }

        return 0;
    }

    public void EnableQps(bool enable)
    {
        this._enable = enable;
    }

    public void Dispose()
    {
    }
}

public sealed class ApiQpsService(IQpsService qpsService) : ServiceBase("/api/v1/Qps")
{
    public async Task GetAsync(HttpContext context, string? serviceId = null)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        qpsService.EnableQps(true);

        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(1000);
            await context.Response.WriteAsync($"data:{JsonSerializer.Serialize(new
            {
                qps = qpsService.GetServiceQps(serviceId),
                now = DateTime.Now.ToString("HH:mm:ss")
            })}\n\n");
            await context.Response.Body.FlushAsync();
        }

        qpsService.EnableQps(false);
    }
}