namespace FastGateway.Dto;

public class PageResultDto<T>
{
    public PageResultDto(long total, List<T> items)
    {
        Total = total;
        Items = items;
    }

    public long Total { get; set; }

    public List<T> Items { get; set; }
}