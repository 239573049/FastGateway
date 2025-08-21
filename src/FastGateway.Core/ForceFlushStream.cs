namespace FastGateway.Entities;


/// <summary>
/// 自动刷新的Stream
/// </summary>
public partial class ForceFlushStream(Stream inner) : DelegatingStream(inner)
{
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        await base.WriteAsync(source, cancellationToken);
        await this.FlushAsync(cancellationToken);
    }

    public override ValueTask DisposeAsync()
    {
        return this.Inner.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        throw new InvalidOperationException($"只能调用{nameof(DisposeAsync)}()方法");
    }
}