using Yarp.ReverseProxy.Model;

namespace FastGateway.Dto;

/// <summary>
///     服务健康状态（/api/v1/server/{id}/health）
/// </summary>
public sealed class ServerHealthDto
{
    public bool Online { get; set; }

    /// <summary>
    ///     是否支持健康检查查询（无 IProxyStateLookup 时为 false）
    /// </summary>
    public bool? Supported { get; set; }

    public DateTime? CheckedAtUtc { get; set; }

    public ClusterHealthDto[]? Clusters { get; set; }
}

public sealed class ClusterHealthDto
{
    public string ClusterId { get; set; } = string.Empty;

    public ClusterHealthCheckDto HealthCheck { get; set; } = new();

    public DestinationHealthDto[] Destinations { get; set; } = Array.Empty<DestinationHealthDto>();
}

public sealed class ClusterHealthCheckDto
{
    public bool Enabled { get; set; }

    public string? Path { get; set; }
}

public sealed class DestinationHealthDto
{
    public string DestinationId { get; set; } = string.Empty;

    public string? Address { get; set; }

    public DestinationHealthStateDto Health { get; set; } = new();
}

public sealed class DestinationHealthStateDto
{
    public DestinationHealth Active { get; set; }

    public DestinationHealth Passive { get; set; }

    public DestinationHealth Effective { get; set; }
}
