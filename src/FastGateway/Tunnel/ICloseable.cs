namespace FastGateway.Tunnel;

internal interface ICloseable
{
    bool IsClosed { get; }
    void Abort();
}