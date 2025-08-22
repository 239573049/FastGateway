namespace Core.Entities;

public sealed class Setting
{
    public string Key { get; set; } = string.Empty;

    private string value;

    public string Value
    {
        get => value;
        set => this.value = value;
    }

    public string? Description { get; set; }

    public bool IsPublic { get; set; }

    public bool IsSystem { get; set; }

    public string? Group { get; set; }
}