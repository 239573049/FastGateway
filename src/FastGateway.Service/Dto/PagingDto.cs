namespace FastGateway.Service.Dto;

public sealed class PagingDto<T>
{
    public int Total { get; set; }

    public List<T> Items { get; set; }

    public PagingDto(int total, List<T> items)
    {
        Total = total;
        Items = items;
    }

    public PagingDto()
    {
        Items = new List<T>();
    }
}