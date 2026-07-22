namespace FastGateway.Dto;

/// <summary>
///     系统版本信息（/api/v1/system/version）
/// </summary>
public sealed class SystemVersionDto
{
    public string Version { get; set; } = string.Empty;

    public string Framework { get; set; } = string.Empty;

    public string Os { get; set; } = string.Empty;
}

/// <summary>
///     网关运行与版本信息（/api/v1/system/info）
/// </summary>
public sealed class SystemInfoDto
{
    public string? Name { get; set; }

    public string Version { get; set; } = string.Empty;

    public string? InformationalVersion { get; set; }

    public string? FileVersion { get; set; }

    public string? Product { get; set; }

    public string? Description { get; set; }

    public string? Company { get; set; }

    public string? YarpVersion { get; set; }

    public string Framework { get; set; } = string.Empty;

    public string Os { get; set; } = string.Empty;

    public string OsArchitecture { get; set; } = string.Empty;

    public string ProcessArchitecture { get; set; } = string.Empty;

    public string MachineName { get; set; } = string.Empty;

    public string? EnvironmentName { get; set; }

    public DateTime ServerTime { get; set; }

    public int? ProcessId { get; set; }

    public DateTime? ProcessStartTime { get; set; }

    public long? UptimeSeconds { get; set; }
}
