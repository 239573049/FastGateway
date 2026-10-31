namespace FastGateway.Dto;

public sealed class StatisticsOverviewDto
{
    /// <summary>统计库是否可用。false 时其余字段均为空，前端应提示服务降级而不是「暂无数据」。</summary>
    public bool Available { get; set; }

    /// <summary>不可用时的可读原因（权限 / 原生库 / SQLite 错误）。</summary>
    public string? UnavailableReason { get; set; }

    public long Requests { get; set; }
    public long PageViews { get; set; }
    public long UniqueVisitors { get; set; }
    public long UniqueIps { get; set; }
    public long Blocked { get; set; }
    public long Blocked403 { get; set; }
    public long Blocked429 { get; set; }
    public long BlockedBot { get; set; }
    public double BlockRate { get; set; }
    public long AttackIps { get; set; }
    public int AbnormalIpsLive { get; set; }
    public long Error4xx { get; set; }
    public double Error4xxRate { get; set; }
    public long Error5xx { get; set; }
    public double Error5xxRate { get; set; }
    public long AvgElapsedMs { get; set; }
    public long DroppedEntries { get; set; }
}

public sealed class TimeSeriesPointDto
{
    public long Time { get; set; }
    public long Requests { get; set; }
    public long PageViews { get; set; }
    public long Blocked { get; set; }
    public long Error4xx { get; set; }
    public long Error5xx { get; set; }
}

public sealed class GeoItemDto
{
    public string Name { get; set; } = string.Empty;
    public long Count { get; set; }
    public long Blocked { get; set; }
    public double Percent { get; set; }
}

public sealed class GeoResultDto
{
    public List<GeoItemDto> Items { get; set; } = [];
    public long Total { get; set; }
}

public sealed class RankingItemDto
{
    public string Key { get; set; } = string.Empty;
    public long Count { get; set; }
    public long Blocked { get; set; }
    public double Percent { get; set; }
}

public sealed class RequestLogItemDto
{
    public long Id { get; set; }
    public long Ts { get; set; }
    public string Host { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public int Status { get; set; }
    public int ElapsedMs { get; set; }
    public string Ip { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public string Os { get; set; } = string.Empty;
    public string Browser { get; set; } = string.Empty;
    public string RefererUrl { get; set; } = string.Empty;
    public int Blocked { get; set; }
}

public sealed class VisitorSummaryDto
{
    public string Ip { get; set; } = string.Empty;
    public long LastVisitAt { get; set; }
    public string Country { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public int Status { get; set; }
    public string RefererHost { get; set; } = string.Empty;
    public string RefererUrl { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool Blocked { get; set; }
    public int BlockReason { get; set; }
    public bool AbnormalIp { get; set; }
    public bool RiskTriggered { get; set; }
    public bool RateLimited { get; set; }
    public long RequestCount { get; set; }
}
