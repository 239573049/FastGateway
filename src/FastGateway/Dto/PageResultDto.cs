namespace FastGateway.Dto;

public class PageResultDto<T>(IReadOnlyList<T> items, int total)
    where T : class
{
    public IReadOnlyList<T> Items { get; set; } = items;

    public int Total { get; set; } = total;
}