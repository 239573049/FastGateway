namespace FastGateway.Entities;

public sealed class ClusterEntity : Entity
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public string ClusterId { get; set; }

    /// <summary>
    ///     集群名称
    /// </summary>
    public string ClusterName { get; set; }

    /// <summary>
    ///     集群描述
    /// </summary>
    public string? Description { get; set; }


    [Column(MapType = typeof(string), StringLength = -1)]
    public List<DestinationsEntity> DestinationsEntities { get; set; } = new();
}

public sealed class DestinationsEntity
{
    public string Id { get; set; }

    public string Address { get; set; }

    public string? Host { get; set; }

    /// <summary>
    ///     接受活动运行状况检查探测的端点。例如<c> http://127.0.0.1:1234 </c>。
    /// </summary>
    public string? Health { get; set; }
}