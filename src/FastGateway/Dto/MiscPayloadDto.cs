namespace FastGateway.Dto;

/// <summary>
///     L4 转发运行状态（/api/v1/stream-forward/{id}/stats）
/// </summary>
public sealed class StreamForwardStatsDto
{
    public bool Online { get; set; }

    public int ActiveConnections { get; set; }

    public int UdpSessions { get; set; }
}

/// <summary>
///     文件内容响应（/api/v1/file-storage/content）
/// </summary>
public sealed class FileContentDto
{
    public string Content { get; set; } = string.Empty;
}

/// <summary>
///     目录列表响应（/api/v1/file-storage/directory）
/// </summary>
public sealed class DirectoryListingDto
{
    public DirectoryInfoDto[] Directories { get; set; } = Array.Empty<DirectoryInfoDto>();

    public FileInfoDto[] Files { get; set; } = Array.Empty<FileInfoDto>();
}

/// <summary>
///     文件/目录属性响应（/api/v1/file-storage/property）
/// </summary>
public sealed class FilePropertyDto
{
    public string Type { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public string? Extension { get; set; }

    public long? Length { get; set; }

    public DateTime CreationTime { get; set; }

    public DateTime LastAccessTime { get; set; }

    public DateTime LastWriteTime { get; set; }
}

/// <summary>
///     通用 Code/Message 响应（网关内部注册、故障转移等场景）
/// </summary>
public sealed class CodeMessageDto
{
    public int Code { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? Error { get; set; }

    public string? Detail { get; set; }
}

/// <summary>
///     代理错误响应（502/503/504），比 <see cref="CodeMessageDto" /> 多请求追踪字段
/// </summary>
public sealed class ProxyErrorDto
{
    public int Code { get; set; }

    public string Message { get; set; } = string.Empty;

    public string? Error { get; set; }

    public string? Detail { get; set; }

    public string RequestId { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    public string? Path { get; set; }
}
