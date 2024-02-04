using System.IO.Pipelines;

namespace FastGateway.Middlewares.FlowAnalytics;

internal sealed class FlowAnalyzeDuplexPipe(IDuplexPipe duplexPipe, IFlowAnalyzer flowAnalyzer)
    : DelegatingDuplexPipe<FlowAnalyzeStream>(duplexPipe, stream => new FlowAnalyzeStream(stream, flowAnalyzer));