namespace FastGateway.Services;

public class RequestSourceService
{
    /// <summary>
    /// 限制队列长度，防止内存泄漏
    /// </summary>
    private readonly Channel<RequestSourceEntity> _channel;

    private readonly IFreeSql _freeSql;

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
    public void ExecuteAsync(RequestSourceEntity entity)
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

        // 搜索是否存在相同的记录
        var source = _freeSql.Select<RequestSourceEntity>().Where(x => x.Ip == entity.Ip).First();

        if (source == null)
        {
            _freeSql.Insert(entity).ExecuteAffrows();
        }
        else
        {
            // 使用sql的原则更新数据
            _freeSql.Ado.ExecuteNonQuery(
                "update RequestSourceEntity set RequestCount = RequestCount + @RequestCount, LastRequestTime = @LastRequestTime, Host = @Host, UserAgent = @UserAgent, Platform = @Platform where Ip = @Ip",
                new
                {
                    entity.RequestCount,
                    entity.LastRequestTime,
                    entity.Host,
                    entity.UserAgent,
                    entity.Platform,
                    entity.Ip
                });
        }
    }
}