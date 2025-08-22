using System.Text.Json;
using System.Text.Json.Serialization;
using TunnelClient.Model;

namespace TunnelClient;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    AllowTrailingCommas = true,
    IgnoreReadOnlyFields = true,
    IgnoreReadOnlyProperties = true,
    IncludeFields = true,
    GenerationMode = JsonSourceGenerationMode.Metadata
)]
[JsonSerializable(typeof(Tunnel))]
internal partial class AppContext : JsonSerializerContext
{
    public static JsonSerializerOptions JsonOptions => new(Default.Options)
    {
        // 允许注释
        ReadCommentHandling = JsonCommentHandling.Skip,
        // 忽略大小写
        PropertyNameCaseInsensitive = true
    };
}