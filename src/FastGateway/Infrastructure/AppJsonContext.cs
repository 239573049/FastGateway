using System.Text.Json.Serialization;
using Core.Entities;
using Core.Entities.Core;
using FastGateway.Dto;
using FastGateway.Services;
using FastGateway.Tunnels;

namespace FastGateway.Infrastructure;

/// <summary>
///     System.Text.Json 源生成上下文（Native AOT）。
///     覆盖所有经 Minimal API / 中间件序列化的类型。ResultFilter 会把返回值装箱到
///     ResultDto.Data(object)，因此每个真实运行时类型都必须在此注册，STJ 才能解析。
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
// ===== 业务结果包装 =====
[JsonSerializable(typeof(ResultDto))]
[JsonSerializable(typeof(ResultDto<object>))]
[JsonSerializable(typeof(object))]
// ===== 配置持久化根 =====
[JsonSerializable(typeof(GatewayConfig))]
// ===== Core 实体 =====
[JsonSerializable(typeof(Server))]
[JsonSerializable(typeof(DomainName))]
[JsonSerializable(typeof(DomainName[]))]
[JsonSerializable(typeof(List<DomainName>))]
[JsonSerializable(typeof(Cert))]
[JsonSerializable(typeof(CertData))]
[JsonSerializable(typeof(List<Cert>))]
[JsonSerializable(typeof(BlacklistAndWhitelist))]
[JsonSerializable(typeof(List<BlacklistAndWhitelist>))]
[JsonSerializable(typeof(RateLimit))]
[JsonSerializable(typeof(List<RateLimit>))]
[JsonSerializable(typeof(Setting))]
[JsonSerializable(typeof(List<Setting>))]
[JsonSerializable(typeof(StreamForward))]
[JsonSerializable(typeof(List<StreamForward>))]
[JsonSerializable(typeof(StreamUpStream))]
[JsonSerializable(typeof(List<StreamUpStream>))]
[JsonSerializable(typeof(UpStream))]
[JsonSerializable(typeof(HeadersView))]
// ===== 枚举 =====
[JsonSerializable(typeof(CertType))]
[JsonSerializable(typeof(ServiceType))]
[JsonSerializable(typeof(RenewStats))]
[JsonSerializable(typeof(StreamProtocol))]
[JsonSerializable(typeof(StreamLoadBalancing))]
[JsonSerializable(typeof(ClientIpSource))]
// ===== 展示 DTO =====
[JsonSerializable(typeof(ServerDto))]
[JsonSerializable(typeof(List<ServerDto>))]
[JsonSerializable(typeof(StreamForwardDto))]
[JsonSerializable(typeof(List<StreamForwardDto>))]
[JsonSerializable(typeof(StreamForwardStatsDto))]
[JsonSerializable(typeof(TunnelNodeDto))]
[JsonSerializable(typeof(List<TunnelNodeDto>))]
[JsonSerializable(typeof(TunnelProxyDto))]
[JsonSerializable(typeof(TunnelProxyDto[]))]
[JsonSerializable(typeof(DashboardDto))]
[JsonSerializable(typeof(DriveInfoDto))]
[JsonSerializable(typeof(DriveInfoDto[]))]
[JsonSerializable(typeof(DirectoryInfoDto))]
[JsonSerializable(typeof(DirectoryInfoDto[]))]
[JsonSerializable(typeof(FileInfoDto))]
[JsonSerializable(typeof(FileInfoDto[]))]
[JsonSerializable(typeof(DirectoryListingDto))]
[JsonSerializable(typeof(FileContentDto))]
[JsonSerializable(typeof(FilePropertyDto))]
// ===== 分页 =====
[JsonSerializable(typeof(PagingDto<AbnormalIpSnapshot>))]
[JsonSerializable(typeof(PagingDto<BlacklistAndWhitelist>))]
[JsonSerializable(typeof(PagingDto<Cert>))]
[JsonSerializable(typeof(PagingDto<RateLimit>))]
[JsonSerializable(typeof(PagingDto<RankingItemDto>))]
[JsonSerializable(typeof(PagingDto<RequestLogItemDto>))]
// ===== 异常 IP =====
[JsonSerializable(typeof(AbnormalIpSnapshot))]
[JsonSerializable(typeof(List<AbnormalIpSnapshot>))]
[JsonSerializable(typeof(AddAbnormalIpToBlacklistInput))]
// ===== 统计 =====
[JsonSerializable(typeof(StatisticsOverviewDto))]
[JsonSerializable(typeof(TimeSeriesPointDto))]
[JsonSerializable(typeof(List<TimeSeriesPointDto>))]
[JsonSerializable(typeof(GeoItemDto))]
[JsonSerializable(typeof(List<GeoItemDto>))]
[JsonSerializable(typeof(GeoResultDto))]
[JsonSerializable(typeof(RankingItemDto))]
[JsonSerializable(typeof(List<RankingItemDto>))]
[JsonSerializable(typeof(RequestLogItemDto))]
[JsonSerializable(typeof(List<RequestLogItemDto>))]
// ===== QPS 实时监控 =====
[JsonSerializable(typeof(QpsRealtimeDto))]
[JsonSerializable(typeof(QpsHistoryPointDto))]
[JsonSerializable(typeof(QpsHistoryPointDto[]))]
[JsonSerializable(typeof(QpsRequestsDto))]
[JsonSerializable(typeof(QpsResponseTimeDto))]
[JsonSerializable(typeof(QpsSystemDto))]
[JsonSerializable(typeof(QpsCpuDto))]
[JsonSerializable(typeof(QpsMemoryDto))]
[JsonSerializable(typeof(QpsDiskDto))]
[JsonSerializable(typeof(QpsServiceDto))]
[JsonSerializable(typeof(QpsUptimeDto))]
// ===== 系统信息 =====
[JsonSerializable(typeof(SystemVersionDto))]
[JsonSerializable(typeof(SystemInfoDto))]
// ===== 服务健康 =====
[JsonSerializable(typeof(ServerHealthDto))]
[JsonSerializable(typeof(ClusterHealthDto))]
[JsonSerializable(typeof(ClusterHealthDto[]))]
[JsonSerializable(typeof(ClusterHealthCheckDto))]
[JsonSerializable(typeof(DestinationHealthDto))]
[JsonSerializable(typeof(DestinationHealthDto[]))]
[JsonSerializable(typeof(DestinationHealthStateDto))]
// ===== 网关内部 / 错误响应 =====
[JsonSerializable(typeof(CodeMessageDto))]
[JsonSerializable(typeof(ProxyErrorDto))]
[JsonSerializable(typeof(Tunnel))]
[JsonSerializable(typeof(Tunnel.TunnelProxy))]
// ===== 输入 DTO =====
[JsonSerializable(typeof(CheckInput))]
[JsonSerializable(typeof(SettingInput))]
[JsonSerializable(typeof(CreateZipRequest))]
[JsonSerializable(typeof(CreateZipFromPathRequest))]
[JsonSerializable(typeof(UnzipRequest))]
[JsonSerializable(typeof(DeleteMultipleRequest))]
[JsonSerializable(typeof(DeleteItem))]
[JsonSerializable(typeof(MoveFileRequest))]
[JsonSerializable(typeof(CopyFileRequest))]
[JsonSerializable(typeof(CreateFileRequest))]
[JsonSerializable(typeof(SaveContentRequest))]
// ===== 基元 =====
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(bool))]
[JsonSerializable(typeof(int))]
[JsonSerializable(typeof(long))]
[JsonSerializable(typeof(double))]
public partial class AppJsonContext : JsonSerializerContext
{
}

/// <summary>
///     配置文件专用源生成上下文：与 <see cref="AppJsonContext" /> 分离仅为开启 WriteIndented，
///     保持 data/gateway.config 人类可读。
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(GatewayConfig))]
public partial class ConfigJsonContext : JsonSerializerContext
{
}
