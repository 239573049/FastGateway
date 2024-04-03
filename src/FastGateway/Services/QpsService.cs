using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

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
    private readonly QpsItem qpsItem = new();
    private bool _enable;

    public void IncrementServiceRequests()
    {
        if (_enable == false)
        {
            return;
        }

        qpsItem.IncrementRequests();
    }

    public void CalculateServiceQps()
    {
        if (_enable == false)
        {
            return;
        }

        qpsItem.CalculateQps();
    }

    public int GetServiceQps()
    {
        var qps = qpsItem.GetQps();

        qpsItem.CalculateQps();

        return qps;
    }

    public void EnableQps(bool enable)
    {
        _enable = enable;
    }

    public void Dispose()
    {
    }
}

public sealed class ApiQpsService
{
    public static async Task GetAsync([FromServices] IQpsService qpsService, HttpContext context,
        string? serviceId = null)
    {
        context.Response.Headers.ContentType = "text/event-stream";
        qpsService.EnableQps(true);

        for (var i = 0; i < 10; i++)
        {
            await Task.Delay(1000);
            await context.Response.WriteAsync($"data:{JsonSerializer.Serialize(new
            {
                qps = qpsService.GetServiceQps(),
                now = DateTime.Now.ToString("HH:mm:ss")
            })}\n\n");
            await context.Response.Body.FlushAsync();
        }

        qpsService.EnableQps(false);
    }
}