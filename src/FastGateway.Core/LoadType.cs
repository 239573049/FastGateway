namespace FastGateway.Core;

public enum LoadType: byte
{
    IpHash = 1,
    
    RoundRobin = 2,
    
    WeightRoundRobin = 3,
    
}