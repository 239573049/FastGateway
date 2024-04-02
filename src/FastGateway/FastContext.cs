namespace FastGateway;

public class FastContext
{
    public static IQpsService QpsService { get; protected set; } = null!;
    
    public static void SetQpsService(IQpsService qpsService)
    {
        QpsService = qpsService;
    }
}