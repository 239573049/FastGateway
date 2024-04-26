using System.Threading.Tasks.Sources;
using Microsoft.AspNetCore.Http;

namespace FastGateway.TunnelServer;


internal class DuplexHttpStream(HttpContext context) : Stream, IValueTaskSource<object?>, ICloseable
{
    private readonly object _sync = new();
    private ManualResetValueTaskSourceCore<object?> _tcs = new() { RunContinuationsAsynchronously = true };

    private Stream RequestBody => context.Request.Body;
    private Stream ResponseBody => context.Response.Body;

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

    public bool IsClosed => context.RequestAborted.IsCancellationRequested;

    public void Abort()
    {
        context.Abort();

        lock (_sync)
        {
            if (GetStatus(_tcs.Version) != ValueTaskSourceStatus.Pending) return;

            _tcs.SetResult(null);
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
        return ResponseBody.WriteAsync(buffer, cancellationToken);
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
        lock (_sync)
        {
            if (GetStatus(_tcs.Version) != ValueTaskSourceStatus.Pending) return;

            // This might seem evil but we're using dispose to know if the stream
            // has been given discarded by http client. We trigger the continuation and take back ownership
            // of it here.
            _tcs.SetResult(null);
        }
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