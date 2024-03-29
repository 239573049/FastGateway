namespace FastGateway.Middlewares.FlowAnalytics;

public interface IFlowAnalyzer
{
    /// <summary>
    ///     收到数据
    /// </summary>
    /// <param name="flowType"></param>
    /// <param name="length"></param>
    void OnFlow(FlowType flowType, int length);

    /// <summary>
    ///     获取速率
    /// </summary>
    /// <returns></returns>
    FlowStatisticsDto GetFlowStatistics();

    /// <summary>
    ///     清楚记录
    /// </summary>
    void CleanRecords();
}