namespace FastGateway.Core;

public interface IQpsService
{
    void IncrementServiceRequests();

    void CalculateServiceQps();

    int GetServiceQps();
    
    /// <summary>
    /// 启用或禁用QPS统计
    /// </summary>
    /// <param name="enable"></param>
    void EnableQps(bool enable);
}