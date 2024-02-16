using FastGateway.Contract;
using Microsoft.AspNetCore.Mvc;

namespace FastGateway.Services;

public sealed class RequestSourceService
{
    /// <summary>
    /// 限制队列长度，防止内存泄漏
    /// </summary>
    private readonly Channel<RequestSourceEntity> _channel;

    private readonly IFreeSql _freeSql;

    private readonly ConcurrentDictionary<uint, RequestSourceEntity> _ipRequestInfo = new();

    public RequestSourceService(IFreeSql freeSql, IHomeAddressService homeAddressService)
    {
        _freeSql = freeSql;
        _channel = Channel.CreateBounded<RequestSourceEntity>(new BoundedChannelOptions(10000)
        {
            SingleReader = true,
            SingleWriter = false
        });
        Task.Run(HandleRequestSourceAsync);
    }

    /// <summary>
    /// 当超出队列长度时，不插入数据
    /// </summary>
    /// <param name="entity"></param>
    public void Execute(RequestSourceEntity entity)
    {
        _channel.Writer.TryWrite(entity);
    }

    private async Task HandleRequestSourceAsync()
    {
        // 一直读取队列中的数据
        while (await _channel.Reader.WaitToReadAsync())
        {
            while (_channel.Reader.TryRead(out var entity))
            {
                try
                {
                    await Task.Factory.StartNew(SaveRequestSourceAsync, entity);
                }
                catch (Exception e)
                {
                    Console.WriteLine("处理请求日志发送错误" + e);
                }
            }
        }
    }

    private void SaveRequestSourceAsync(object? o)
    {
        if (o is not RequestSourceEntity entity) return;

        // 通过安全集合更新数据
        _ipRequestInfo.AddOrUpdate(IpToInt(entity.Ip), entity, (key, oldValue) =>
        {
            oldValue.RequestCount++;
            return oldValue;
        });
    }

    /// <summary>
    /// 获取并且清空数据
    /// </summary>
    /// <returns></returns>
    public List<RequestSourceEntity> GetAndClearDataAsync()
    {
        var list = _ipRequestInfo.Values.ToList();
        _ipRequestInfo.Clear();
        return list;
    }

    /// <summary>
    /// 将ip字符串转换最小的整数
    /// </summary>
    /// <param name="ip"></param>
    /// <returns></returns>
    public static uint IpToInt(string ip)
    {
        var ipParts = ip.Split(["."], StringSplitOptions.None);
        uint ipInInt = 0;

        for (var i = 0; i < 4; i++)
        {
            ipInInt = (ipInInt << 8) + Convert.ToUInt32(ipParts[i]);
        }

        return ipInInt;
    }

    public object GetDisplayData()
    {
        var now = DateTime.Now.AddDays(-7);

        var requestSourceDto = new RequestSourceDto();

        var result = _freeSql.Select<RequestSourceEntity>()
            .Where(x => x.CreatedTime >= now)
            .GroupBy(x => x.CreatedTime.ToString("yyyy-MM-dd"))
            .Select(x => new RequestSourceDayCountDto
            {
                Day = x.Key,
                Count = x.Count()
            }).OrderBy(x => x.Day).ToList();

        requestSourceDto.DayCountDtos = result;

        var sourceResult = _freeSql.Select<RequestSourceEntity>()
            .Where(x => x.CreatedTime >= now && !string.IsNullOrEmpty(x.HomeAddress))
            .GroupBy(x => x.HomeAddress)
            .Select(x => new RequestSourceAddressDto
            {
                HomeAddress = x.Key,
                Count = x.Count()
            }).OrderByDescending(x => x.Count).ToList();

        requestSourceDto.AddressDtos = sourceResult;

        return requestSourceDto;
    }
}

public static class RequestSourceExtension
{
    public static void MapRequestSource(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/gateway/request-source/display-data",
            ([FromServices] RequestSourceService requestSourceService) =>
                requestSourceService.GetDisplayData());
    }
}