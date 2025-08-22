using System.Threading.Channels;
using System.Threading.Tasks.Sources;

namespace FastGateway.Tunnels;

internal class DuplexHttpStream : Stream, IValueTaskSource<object?>, ICloseable
{
    private readonly HttpContext _context;
    private readonly Task _processWriteTask;
    private readonly object _sync = new();

    // 用于队列写入操作的通道
    private readonly Channel<ReadOnlyMemory<byte>> _writeChannel;

    private ManualResetValueTaskSourceCore<object?> _tcs = new() { RunContinuationsAsynchronously = true };

    public DuplexHttpStream(HttpContext context)
    {
        _context = context;

        // 初始化写入通道
        _writeChannel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>(new UnboundedChannelOptions
        {
            SingleReader = true, // 只有一个读取者处理写入
            SingleWriter = false, // 允许多个写入者并发地加入数据
            AllowSynchronousContinuations = false
        });

        // 启动后台任务处理写入队列
        _processWriteTask = Task.Run(ProcessWriteQueueAsync);

        _context.RequestAborted.Register(() =>
        {
            lock (_sync)
            {
                if (GetStatus(_tcs.Version) != ValueTaskSourceStatus.Pending) return;

                _tcs.SetResult(null);
            }

            // 当请求被中止时，完成写入通道
            _writeChannel.Writer.TryComplete(new OperationCanceledException("请求已中止。"));
        });
    }

    private Stream RequestBody => _context.Request.Body;
    private Stream ResponseBody => _context.Response.Body;

    internal ValueTask<object?> StreamCompleteTask => new(this, _tcs.Version);

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => true;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public bool IsClosed => _context.RequestAborted.IsCancellationRequested;

    public void Abort()
    {
        _context.Abort();
        _writeChannel.Writer.TryComplete(new OperationCanceledException("流已中止。"));

        lock (_sync)
        {
            if (GetStatus(_tcs.Version) != ValueTaskSourceStatus.Pending) return;

            _tcs.SetException(new OperationCanceledException("流已中止。"));
        }
    }

    public object? GetResult(short token)
    {
        return _tcs.GetResult(token);
    }

    public ValueTaskSourceStatus GetStatus(short token)
    {
        return _tcs.GetStatus(token);
    }

    public void OnCompleted(Action<object?> continuation, object? state, short token,
        ValueTaskSourceOnCompletedFlags flags)
    {
        _tcs.OnCompleted(continuation, state, token, flags);
    }

    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        // 将缓冲区加入写入通道，支持取消操作
        if (!_writeChannel.Writer.TryWrite(buffer)) return _writeChannel.Writer.WriteAsync(buffer, cancellationToken);

        return ValueTask.CompletedTask;
    }

    // 在后台任务中处理写入队列
    private async Task ProcessWriteQueueAsync()
    {
        try
        {
            await foreach (var buffer in _writeChannel.Reader.ReadAllAsync())
                // 写入到 ResponseBody
                await ResponseBody.WriteAsync(buffer);
        }
        catch (Exception ex)
        {
            // 处理异常
            Console.WriteLine(ex);
            lock (_sync)
            {
                if (GetStatus(_tcs.Version) != ValueTaskSourceStatus.Pending) return;

                _tcs.SetException(ex);
            }
        }
        finally
        {
            // 确保刷新 ResponseBody
            await ResponseBody.FlushAsync();
            // 通知写入已完成
            lock (_sync)
            {
                if (GetStatus(_tcs.Version) == ValueTaskSourceStatus.Pending) _tcs.SetResult(null);
            }
        }
    }

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        return RequestBody.ReadAsync(buffer, cancellationToken);
    }

    public void Reset()
    {
        _tcs.Reset();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 完成写入通道，表示写入结束
            _writeChannel.Writer.TryComplete();
            // 等待后台任务完成
            _processWriteTask.GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }

    public override void Flush()
    {
        throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }
}