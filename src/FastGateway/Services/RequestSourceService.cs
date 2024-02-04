namespace FastGateway.Services;

public sealed class RequestSourceService
{
    /// <summary>
    /// 限制队列长度，防止内存泄漏
    /// </summary>
    private readonly Channel<RequestSourceEntity> _channel;

    private readonly IFreeSql _freeSql;

    private readonly ConcurrentDictionary<uint, WeakReference> _ipLocks = new();

    private readonly Dictionary<uint, (RequestSourceEntity Entity, DateTime)> _ipRequestInfo = new();

    public RequestSourceService(IFreeSql freeSql)
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


    private async Task SaveRequestSourceAsync(object? o)
    {
        if (o is not RequestSourceEntity entity) return;


        var weakRef = _ipLocks.GetOrAdd(IpToInt(entity.Ip), new WeakReference(new object()));
        var ipLock = weakRef.Target;
        if (ipLock == null)
        {
            // The lock was garbage collected, create a new one
            ipLock = new object();
            weakRef.Target = ipLock;
        }

        lock (ipLock)
        {
            if (_ipRequestInfo.TryGetValue(IpToInt(entity.Ip), out var info))
            {
                // 指定时间内只记录一次请求，防止数据库压力过大
                if (DateTime.Now - info.Item2 > TimeSpan.FromSeconds(10))
                {
                    _ipRequestInfo.Remove(IpToInt(entity.Ip));
                }
                // 如果请求时间间隔小于3秒，则不记录，只更新请求次数
                else
                {
                    info.Entity.RequestCount++;
                    return;
                }
            }

            _ipRequestInfo[IpToInt(entity.Ip)] = (entity, DateTime.Now);
        }

        // 搜索是否存在相同的记录
        var source = await _freeSql.Select<RequestSourceEntity>().Where(x => x.Ip == entity.Ip).FirstAsync();

        try
        {
            if (source == null)
            {
                await _freeSql.Insert(entity).ExecuteAffrowsAsync();
            }
            else
            {
                // 使用sql的原则更新数据
                await _freeSql.Ado.ExecuteNonQueryAsync(
                    "update RequestSourceEntity set RequestCount = RequestCount+1, LastRequestTime = @LastRequestTime, Host = @Host,  Platform = @Platform where Ip = @Ip",
                    new
                    {
                        LastRequestTime = DateTime.Now,
                        entity.Host,
                        entity.Platform,
                        entity.Ip
                    });
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public static uint IpToInt(string ip)
    {
        var ipParts = ip.Split(new[] { "." }, StringSplitOptions.None);
        uint ipInInt = 0;

        for (var i = 0; i < 4; i++)
        {
            ipInInt = (ipInInt << 8) + Convert.ToUInt32(ipParts[i]);
        }

        return ipInInt;
    }
}