using System.Buffers;
using System.IO.Pipelines;
using System.Threading.Tasks.Sources;
using Microsoft.AspNetCore.Connections;

namespace FastGateway.TunnelServer;

internal class ConnectionContextStream(ConnectionContext connectionContext) : Stream, IValueTaskSource<object?>
{
    private readonly object _sync = new();
    private ManualResetValueTaskSourceCore<object?> _tcs = new() { RunContinuationsAsynchronously = true };

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

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        await connectionContext.Transport.Output.WriteAsync(buffer, cancellationToken);
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var result = await connectionContext.Transport.Input.ReadAsync(cancellationToken).ConfigureAwait(false);
        return HandleReadResult(result, buffer.Span);
    }

    private int HandleReadResult(ReadResult result, Span<byte> buffer)
    {
        if (result.IsCanceled) throw new OperationCanceledException();

        var sequence = result.Buffer;
        var bufferLength = sequence.Length;
        var consumed = sequence.Start;

        try
        {
            if (bufferLength != 0)
            {
                var actual = (int)Math.Min(bufferLength, buffer.Length);

                var slice = actual == bufferLength ? sequence : sequence.Slice(0, actual);
                consumed = slice.End;
                slice.CopyTo(buffer);

                return actual;
            }

            if (result.IsCompleted) return 0;
        }
        finally
        {
            connectionContext.Transport.Input.AdvanceTo(consumed);
        }

        return 0;
    }

    public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        // Delegate to CopyToAsync on the PipeReader
        return connectionContext.Transport.Input.CopyToAsync(destination, cancellationToken);
    }

    internal void Shutdown()
    {
        connectionContext.Abort();

        lock (_sync)
        {
            if (GetStatus(_tcs.Version) != ValueTaskSourceStatus.Pending) return;

            _tcs.SetResult(null);
        }
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

    public void Reset()
    {
        _tcs.Reset();
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