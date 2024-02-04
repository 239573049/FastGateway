using System.IO.Pipelines;

namespace FastGateway.Middlewares.FlowAnalytics;

/// <summary>
///     基于委托流的DuplexPipe
/// </summary>
/// <typeparam name="TDelegatingStream"></typeparam>
public class DelegatingDuplexPipe<TDelegatingStream> : IDuplexPipe, IAsyncDisposable
    where TDelegatingStream : DelegatingStream
{
    private readonly object _syncRoot = new();
    private bool _disposed;

    /// <summary>
    ///     基于委托流的DuplexPipe
    /// </summary>
    /// <param name="duplexPipe"></param>
    /// <param name="delegatingStreamFactory">委托流工厂</param>
    public DelegatingDuplexPipe(IDuplexPipe duplexPipe, Func<Stream, TDelegatingStream> delegatingStreamFactory) :
        this(duplexPipe, delegatingStreamFactory, new StreamPipeReaderOptions(leaveOpen: true),
            new StreamPipeWriterOptions(leaveOpen: true))
    {
    }

    /// <summary>
    ///     基于委托流的DuplexPipe
    /// </summary>
    /// <param name="duplexPipe"></param>
    /// <param name="delegatingStreamFactory">委托流工厂</param>
    /// <param name="readerOptions"></param>
    /// <param name="writerOptions"></param>
    public DelegatingDuplexPipe(IDuplexPipe duplexPipe, Func<Stream, TDelegatingStream> delegatingStreamFactory,
        StreamPipeReaderOptions readerOptions, StreamPipeWriterOptions writerOptions)
    {
        var duplexPipeStream = new DuplexPipeStream(duplexPipe);
        var delegatingStream = delegatingStreamFactory(duplexPipeStream);
        Input = PipeReader.Create(delegatingStream, readerOptions);
        Output = PipeWriter.Create(delegatingStream, writerOptions);
    }

    /// <summary>
    ///     释放资源
    /// </summary>
    /// <returns></returns>
    public virtual async ValueTask DisposeAsync()
    {
        lock (_syncRoot)
        {
            if (_disposed) return;
            _disposed = true;
        }

        await Input.CompleteAsync();
        await Output.CompleteAsync();
    }

    /// <summary>
    ///     输入对象
    /// </summary>
    public PipeReader Input { get; }

    /// <summary>
    ///     输出对象
    /// </summary>
    public PipeWriter Output { get; }
}