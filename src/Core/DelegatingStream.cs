﻿namespace Core;


/// <summary>
/// 委托流
/// </summary>
public abstract class DelegatingStream : Stream
{
    /// <summary>
    /// 获取所包装的流对象
    /// </summary>
    protected readonly Stream Inner;

    /// <summary>
    /// 委托流
    /// </summary>
    /// <param name="inner"></param>
    public DelegatingStream(Stream inner)
    {
        this.Inner = inner;
    }

    /// <inheritdoc/>
    public override bool CanRead => Inner.CanRead;

    /// <inheritdoc/>
    public override bool CanSeek => Inner.CanSeek;

    /// <inheritdoc/>
    public override bool CanWrite => Inner.CanWrite;

    /// <inheritdoc/>
    public override long Length => Inner.Length;

    /// <inheritdoc/>
    public override bool CanTimeout => Inner.CanTimeout;

    /// <inheritdoc/>
    public override int ReadTimeout
    {
        get => Inner.ReadTimeout;
        set => Inner.ReadTimeout = value;
    }

    /// <inheritdoc/>
    public override int WriteTimeout
    {
        get => Inner.WriteTimeout;
        set => Inner.WriteTimeout = value;
    }


    /// <inheritdoc/>
    public override long Position
    {
        get => Inner.Position;
        set => Inner.Position = value;
    }

    /// <inheritdoc/>
    public override void Flush()
    {
        Inner.Flush();
    }

    /// <inheritdoc/>
    public override Task FlushAsync(CancellationToken cancellationToken)
    {
        return Inner.FlushAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        return Inner.Read(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override int Read(Span<byte> destination)
    {
        return Inner.Read(destination);
    }

    /// <inheritdoc/>
    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return Inner.ReadAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc/>
    public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
    {
        return Inner.ReadAsync(destination, cancellationToken);
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin)
    {
        return Inner.Seek(offset, origin);
    }

    /// <inheritdoc/>
    public override void SetLength(long value)
    {
        Inner.SetLength(value);
    }

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count)
    {
        Inner.Write(buffer, offset, count);
    }

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> source)
    {
        Inner.Write(source);
    }

    /// <inheritdoc/>
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return Inner.WriteAsync(buffer, offset, count, cancellationToken);
    }

    /// <inheritdoc/>
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        return Inner.WriteAsync(source, cancellationToken);
    }

    /// <inheritdoc/>
    public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state)
    {
        return TaskToAsyncResult.Begin(ReadAsync(buffer, offset, count), callback, state);
    }

    /// <inheritdoc/>
    public override int EndRead(IAsyncResult asyncResult)
    {
        return TaskToAsyncResult.End<int>(asyncResult);
    }

    /// <inheritdoc/>
    public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback,
        object? state)
    {
        return TaskToAsyncResult.Begin(WriteAsync(buffer, offset, count), callback, state);
    }

    /// <inheritdoc/>
    public override void EndWrite(IAsyncResult asyncResult)
    {
        TaskToAsyncResult.End(asyncResult);
    }

    /// <inheritdoc/>
    public override int ReadByte()
    {
        return Inner.ReadByte();
    }

    /// <inheritdoc/>
    public override void WriteByte(byte value)
    {
        Inner.WriteByte(value);
    }

    /// <inheritdoc/>
    public sealed override void Close()
    {
        base.Close();
    }
}