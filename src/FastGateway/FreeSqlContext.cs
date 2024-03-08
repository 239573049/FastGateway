namespace FastGateway;

public class FreeSqlContext
{
    private static IFreeSql _freeSql;
    
    public static IFreeSql FreeSql => _freeSql;

    public static void Initialize(IFreeSql freeSql)
    {
        _freeSql = freeSql;
    }
}