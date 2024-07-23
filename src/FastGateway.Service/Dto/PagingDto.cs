namespace FastGateway.Service.Dto;

public sealed class PagingDto<T>
{
    public int Total { get; set; }

    public List<T> Itmes { get; set; }

    public PagingDto(int total, List<T> items)
    {
        Total = total;
        Itmes = items;
    }

    public PagingDto()
    {
        Itmes = new List<T>();
    }
}