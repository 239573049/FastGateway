using System.IO.Pipelines;

namespace Gateway.Middlewares.FlowAnalytics;

sealed class FlowAnalyzeDuplexPipe(IDuplexPipe duplexPipe, IFlowAnalyzer flowAnalyzer)
    : DelegatingDuplexPipe<FlowAnalyzeStream>(duplexPipe, stream => new FlowAnalyzeStream(stream, flowAnalyzer));