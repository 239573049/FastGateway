namespace FastGateway.Service.Tunnels;

internal interface ICloseable
{
    bool IsClosed { get; }
    void Abort();
}