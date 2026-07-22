namespace FastGateway.Dto;

/// <summary>
///     实时 QPS 监控数据（替代原匿名对象，供 AOT 源生成序列化）
/// </summary>
public sealed class QpsRealtimeDto
{
    public int Qps { get; set; }

    public QpsHistoryPointDto[] QpsHistory { get; set; } = Array.Empty<QpsHistoryPointDto>();

    public string Now { get; set; } = string.Empty;

    public QpsRequestsDto Requests { get; set; } = new();

    public QpsResponseTimeDto ResponseTime { get; set; } = new();

    public QpsSystemDto System { get; set; } = new();

    public QpsServiceDto Service { get; set; } = new();
}

public sealed class QpsHistoryPointDto
{
    public string Time { get; set; } = string.Empty;

    public int Qps { get; set; }
}

public sealed class QpsRequestsDto
{
    public long Total { get; set; }

    public long Success { get; set; }

    public long Failed { get; set; }

    public double SuccessRate { get; set; }
}

public sealed class QpsResponseTimeDto
{
    public long Avg { get; set; }

    public long P95 { get; set; }

    public long P99 { get; set; }

    public long Min { get; set; }

    public long Max { get; set; }
}

public sealed class QpsSystemDto
{
    public QpsCpuDto Cpu { get; set; } = new();

    public QpsMemoryDto Memory { get; set; } = new();

    public QpsDiskDto Disk { get; set; } = new();
}

public sealed class QpsCpuDto
{
    public double Usage { get; set; }

    public int Cores { get; set; }
}

public sealed class QpsMemoryDto
{
    public double Usage { get; set; }

    public long Total { get; set; }

    public long Used { get; set; }

    public long Available { get; set; }

    public long WorkingSet { get; set; }

    public long Gc { get; set; }
}

public sealed class QpsDiskDto
{
    public long ReadBytesPerSec { get; set; }

    public long WriteBytesPerSec { get; set; }
}

public sealed class QpsServiceDto
{
    public bool IsOnline { get; set; }

    public QpsUptimeDto Uptime { get; set; } = new();

    public string LastUpdate { get; set; } = string.Empty;
}

public sealed class QpsUptimeDto
{
    public int Days { get; set; }

    public int Hours { get; set; }

    public int Minutes { get; set; }

    public int Seconds { get; set; }

    public long TotalSeconds { get; set; }
}
