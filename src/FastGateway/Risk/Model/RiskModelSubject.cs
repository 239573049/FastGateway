namespace FastGateway.Risk.Model;

/// <summary>
///     等待模型评估的来源与入队时的特征快照
/// </summary>
public sealed record RiskModelSubject(string Ip, string? Host, RiskFeatureState Features, long QueuedAtTicks);

public interface IRiskModelSink
{
    /// <summary>投递评估请求，队列已满时返回 false 并直接丢弃（风控回落到规则）</summary>
    bool TryEnqueue(RiskModelSubject subject);
}
