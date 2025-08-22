namespace Core;

public class SafeWriteStream(Stream inner) : DelegatingStream(inner)
{
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
    {
        try
        {
            await this._semaphoreSlim.WaitAsync(CancellationToken.None);
            await base.WriteAsync(source, cancellationToken);
            await this.FlushAsync(cancellationToken);
        }
        finally
        {
            this._semaphoreSlim.Release();
        }
    }

    public override ValueTask DisposeAsync()
    {
        this._semaphoreSlim.Dispose();
        return this.Inner.DisposeAsync();
    }

    protected override void Dispose(bool disposing)
    {
        this._semaphoreSlim.Dispose();
        this.Inner.Dispose();
    }
}