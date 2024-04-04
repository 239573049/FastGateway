namespace FastGateway;

public static class FastContext
{
    public static IQpsService QpsService { get; private set; } = null!;
    
    public static IMemoryCache MemoryCache { get; private set; } = null!;
    
    public static void SetQpsService(IQpsService qpsService, IMemoryCache memoryCache)
    {
        QpsService = qpsService;
        MemoryCache = memoryCache;
    }
}