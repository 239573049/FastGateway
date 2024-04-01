namespace FastGateway.Infrastructures;

public sealed class CurrentContext : ICurrentContext
{
    public string ServiceId { get; init; }
}