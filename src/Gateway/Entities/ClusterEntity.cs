namespace Gateway.Entities;

public class ClusterEntity
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public string ClusterId { get; set; }

    public List<DestinationsEntity> DestinationsEntities { get; set; } = new();
}

public class DestinationsEntity
{
    public string Id { get; init; }
    
    public string Address { get; init; }
    
    public string? Host { get; init; }
}