namespace Gateway.Dto;

public class PageResultDto<T>
{
    public long Total { get; set; }
    
    public List<T> Items { get; set; }
    
    public PageResultDto(long total, List<T> items)
    {
        Total = total;
        Items = items;
    }
}