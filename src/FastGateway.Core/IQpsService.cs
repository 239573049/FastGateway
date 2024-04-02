namespace FastGateway.Core;

public interface IQpsService
{
    void AddServiceQps(string serviceId);
    
    void IncrementServiceRequests(string serviceId);

    void CalculateServiceQps(object? state);

    int GetServiceQps(string? serviceId);
    
    /// <summary>
    /// 启用或禁用QPS统计
    /// </summary>
    /// <param name="enable"></param>
    void EnableQps(bool enable);
}