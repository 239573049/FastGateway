namespace FastGateway.Dto;

public class PageResultDto<T>(IReadOnlyList<T> items, long total)
    where T : class
{
    public IReadOnlyList<T> Items { get; set; } = items;

    public long Total { get; set; } = total;
}