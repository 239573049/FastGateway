namespace FastGateway.Entities;

public abstract class Entity : CreatedEntity
{
    [Column(IsIdentity = true, IsPrimary = true)]
    public string Id { get; set; } = null!;
}

public abstract class CreatedEntity
{
    /// <summary>
    ///     创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; }
}