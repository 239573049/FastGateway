namespace FastGateway.TunnelServer;

internal interface ICloseable
{
    bool IsClosed { get; }
    void Abort();
}