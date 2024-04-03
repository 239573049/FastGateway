namespace FastGateway;

public static class FastContext
{
    public static IQpsService QpsService { get; private set; } = null!;
    
    public static void SetQpsService(IQpsService qpsService)
    {
        QpsService = qpsService;
    }
}