namespace Gateway.Entities;

public class ClusterEntity
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public string ClusterId { get; set; }

    /// <summary>
    /// 集群名称
    /// </summary>
    public string ClusterName { get; set; }
    
    /// <summary>
    /// 集群描述
    /// </summary>
    public string? Description { get; set; }
    
    [Column(MapType = typeof(string), StringLength = -1)]
    public List<DestinationsEntity> DestinationsEntities { get; set; } = new();
}

public class DestinationsEntity
{
    public string Id { get; set; }
    
    public string Address { get; set; }
    
    public string? Host { get; set; }
}