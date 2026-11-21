using System.ComponentModel.DataAnnotations;
using Core.Entities;
using FastGateway.Services;

namespace FastGateway.Gateway;

/// <summary>Origin rules shared by gateway CORS and WebSocket requests.</summary>
public sealed class OriginPolicy
{
    private readonly Rule[] _rules;
    public string[] Origins { get; }

    public OriginPolicy(IEnumerable<string> origins)
    {
        var values = origins.Select(value => value?.Trim() ?? "").ToArray();
        if (values.Length > 200) throw new ValidationException("最多允许 200 个来源");
        var rules = new List<Rule>();
        foreach (var value in values)
        {
            if (value.Length > 512 || !TryParseRule(value, out var rule))
                throw new ValidationException($"无效的来源配置：{value}");
            rules.Add(rule!);
        }

        Origins = values;
        _rules = rules.ToArray();
    }

    public bool IsAllowed(string origin)
    {
        if (!TryParseUri(origin, out var uri)) return false;
        return _rules.Any(rule =>
            (rule.Scheme == null || rule.Scheme == uri.Scheme) &&
            (rule.Port == null || rule.Port == uri.Port) &&
            (rule.Wildcard
                ? uri.Host.Length > rule.Host.Length + 1 &&
                  uri.Host.EndsWith("." + rule.Host, StringComparison.OrdinalIgnoreCase)
                : string.Equals(uri.Host, rule.Host, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool TryParseRule(string value, out Rule? rule)
    {
        rule = null;
        var bareWildcard = value.StartsWith("*.", StringComparison.Ordinal);
        if (bareWildcard && value.Contains(':')) return false;
        var wildcard = bareWildcard || value.StartsWith("http://*.", StringComparison.OrdinalIgnoreCase) ||
                       value.StartsWith("https://*.", StringComparison.OrdinalIgnoreCase);
        var input = bareWildcard ? "https://placeholder." + value[2..] :
            wildcard ? value.Replace("*.", "placeholder.", StringComparison.Ordinal) : value;
        if (!TryParseUri(input, out var uri)) return false;
        var host = uri.Host;
        if (wildcard)
        {
            const string prefix = "placeholder.";
            if (!host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
            host = host[prefix.Length..];
            if (Uri.CheckHostName(host) != UriHostNameType.Dns || !host.Contains('.')) return false;
        }

        rule = new Rule(bareWildcard ? null : uri.Scheme, host, bareWildcard ? null : uri.Port, wildcard);
        return true;
    }

    private static bool TryParseUri(string value, out Uri uri)
    {
        uri = null!;
        if (string.IsNullOrEmpty(value) || value.Any(char.IsWhiteSpace) || value.Contains('\\')) return false;
        var separator = value.IndexOf("://", StringComparison.Ordinal);
        if (separator <= 0 || value.AsSpan(separator + 3).IndexOfAny('/', '?', '#') >= 0) return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed) ||
            parsed.Scheme is not ("http" or "https") || string.IsNullOrEmpty(parsed.Host) ||
            !string.IsNullOrEmpty(parsed.UserInfo) || parsed.Host.Contains('*')) return false;
        uri = parsed;
        return true;
    }

    private sealed record Rule(string? Scheme, string Host, int? Port, bool Wildcard);
}

/// <summary>Loads the configured origins and publishes immutable snapshots on changes.</summary>
public static class OriginPolicyStore
{
    public const string SettingKey = "gateway_allowed_origins";
    private static ConfigurationService? _config;
    private static string[] _fallback = ["https://gateway.towuhu.com:12301"];
    private static OriginPolicy _current = new(["https://gateway.towuhu.com:12301"]);

    public static void Initialize(ConfigurationService config, IConfiguration configuration)
    {
        _config = config;
        var configured = configuration.GetSection("Cors:AllowedOrigins").GetChildren()
            .Select(child => child.Value).Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!).ToArray();
        if (configured.Length > 0) _fallback = configured;
        ConfigurationService.ConfigurationChanged += Load;
        Load();
    }

    public static bool IsAllowed(string origin) => Volatile.Read(ref _current).IsAllowed(origin);

    public static IApplicationBuilder UseWebSocketOriginPolicy(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.WebSockets.IsWebSocketRequest &&
                context.Request.Headers.TryGetValue("Origin", out var origin) &&
                !string.IsNullOrEmpty(origin.ToString()) &&
                (origin.Count != 1 || !IsAllowed(origin.ToString())))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context);
        });

    public static OriginSettingsResponse Get()
    {
        var value = _config?.GetSetting(SettingKey);
        return new OriginSettingsResponse(Volatile.Read(ref _current).Origins.ToArray(), value != null);
    }

    public static void Save(ConfigurationService config, string[]? origins)
    {
        if (origins == null) throw new ValidationException("请提供来源列表");
        var policy = new OriginPolicy(origins);
        config.SetManagedSetting(SettingKey, new Setting
        {
            Key = SettingKey,
            Value = string.Join('\n', policy.Origins),
            Description = "网关跨域来源",
            IsSystem = true,
            IsPublic = false,
            Group = "gateway"
        });
    }

    public static void Reset(ConfigurationService config) => config.SetManagedSetting(SettingKey, null);

    private static void Load()
    {
        var setting = _config?.GetSetting(SettingKey);
        try
        {
            if (setting != null && setting.Value == null)
                throw new ValidationException("跨域来源配置不能为空值");
            var origins = setting == null ? _fallback : setting.Value.Length == 0 ? [] : setting.Value.Split('\n');
            Volatile.Write(ref _current, new OriginPolicy(origins));
        }
        catch (ValidationException ex)
        {
            Console.Error.WriteLine($"跨域来源配置无效，拒绝所有带 Origin 的跨域请求：{ex.Message}");
            Volatile.Write(ref _current, new OriginPolicy([]));
        }
    }
}

/// <summary>Effective origin rules and whether they were set in the dashboard.</summary>
public sealed record OriginSettingsResponse(string[] Origins, bool IsOverride);

/// <summary>Origin rules to persist in the dashboard.</summary>
public sealed record UpdateOriginSettingsRequest(string[]? Origins);
