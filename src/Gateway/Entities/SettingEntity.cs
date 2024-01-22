namespace Gateway.Entities;

public sealed class SettingEntity : Entity
{
    /// <summary>
    /// 设置名称
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// 显示名称
    /// </summary>
    public string DisplayName { get; set; } = null!;

    /// <summary>
    /// 设置描述
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// 默认值
    /// </summary>
    public string? DefaultValue { get; set; }

    public Dictionary<string, string> ExtraProperties { get; set; } = new();
}