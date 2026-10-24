using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Options;
using Microsoft.Extensions.Options;

namespace FastGateway.Services;

public enum BotVerificationStatus
{
    Success,
    Rejected,
    Unavailable,
    Misconfigured
}

public readonly record struct BotVerificationResult(BotVerificationStatus Status);

public sealed class BotProtectionService
{
    public const string HttpClientName = "FastGateway.Turnstile";
    public const string ClearanceCookieName = "__FastGateway_BotClearance";
    public const string ProtectionMetadataKey = "FastGateway.BotProtection";
    public const string RouteIdMetadataKey = "FastGateway.BotRouteId";
    public const string RoutePathMetadataKey = "FastGateway.BotRoutePath";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BotProtectionOptions _options;
    private readonly ConcurrentDictionary<string, long> _consumedChallengeStates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, VerifyRateWindow> _verifyRateWindows = new(StringComparer.Ordinal);

    public BotProtectionService(IHttpClientFactory httpClientFactory, IOptions<BotProtectionOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public bool AdminLoginEnabled => _options.EnabledForAdminLogin;

    public static Dictionary<string, string> CreateRouteMetadata(
        DomainName domainName,
        string routePath,
        IReadOnlyDictionary<string, string>? existing = null)
    {
        var metadata = existing == null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(existing, StringComparer.Ordinal);

        if (!domainName.EnableBotProtection)
            return metadata;

        metadata[ProtectionMetadataKey] = "true";
        metadata[RouteIdMetadataKey] = domainName.Id;
        metadata[RoutePathMetadataKey] = routePath;
        return metadata;
    }

    public bool IsTurnstileConfigured =>
        !string.IsNullOrWhiteSpace(_options.SiteKey) &&
        !string.IsNullOrWhiteSpace(_options.SecretKey) &&
        Uri.TryCreate(_options.VerifyEndpoint, UriKind.Absolute, out var endpoint) &&
        endpoint.Scheme == Uri.UriSchemeHttps;

    public bool IsClearanceConfigured => IsTurnstileConfigured && TryGetSigningKey(out _);

    public BotChallengeConfigDto GetAdminChallengeConfig()
    {
        return new BotChallengeConfigDto
        {
            Enabled = _options.EnabledForAdminLogin,
            Configured = IsTurnstileConfigured,
            SiteKey = _options.SiteKey
        };
    }

    public string CreateChallengeState(
        string host,
        string routeId,
        string returnUrl,
        string routePath,
        string action)
    {
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var lifetime = GetChallengeLifetime();
        var state = new BotChallengeState
        {
            Host = NormalizeHost(host),
            RouteId = routeId,
            ReturnUrl = returnUrl,
            RoutePath = NormalizeCookiePath(routePath),
            Action = action,
            IssuedAt = issuedAt,
            ExpiresAt = issuedAt + (long)lifetime.TotalSeconds,
            Nonce = CreateNonce()
        };

        return Sign(state, AppJsonContext.Default.BotChallengeState);
    }

    public bool TryReadChallengeState(
        string? encoded,
        string currentHost,
        out BotChallengeState state)
    {
        state = new BotChallengeState();
        if (!TryReadSigned(encoded, AppJsonContext.Default.BotChallengeState, out state)) return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return state.IssuedAt <= now + 60 &&
               state.ExpiresAt >= now &&
               !string.IsNullOrWhiteSpace(state.RouteId) &&
               !string.IsNullOrWhiteSpace(state.Action) &&
               !string.IsNullOrWhiteSpace(state.Nonce) &&
               !string.IsNullOrWhiteSpace(state.RoutePath) &&
               string.Equals(state.Host, NormalizeHost(currentHost), StringComparison.OrdinalIgnoreCase) &&
               IsSafeReturnUrl(state.ReturnUrl);
    }

    public bool TryConsumeChallengeState(
        string? encoded,
        string currentHost,
        out BotChallengeState state)
    {
        if (!TryReadChallengeState(encoded, currentHost, out state)) return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        CleanupConsumedStates(now);
        return _consumedChallengeStates.TryAdd(state.Nonce, state.ExpiresAt);
    }

    public string CreateClearance(string host, string routeId, string routePath, string? clientIp)
    {
        var issuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var lifetime = GetLifetime();
        var payload = new BotClearancePayload
        {
            Host = NormalizeHost(host),
            RouteId = routeId,
            RoutePath = NormalizeCookiePath(routePath),
            IssuedAt = issuedAt,
            ExpiresAt = issuedAt + (long)lifetime.TotalSeconds,
            Ip = _options.BindClearanceToIp ? clientIp : null,
            Nonce = CreateNonce()
        };

        return Sign(payload, AppJsonContext.Default.BotClearancePayload);
    }

    public bool ValidateClearance(
        string? encoded,
        string host,
        string routeId,
        string routePath,
        string? clientIp)
    {
        if (!TryReadSigned(encoded, AppJsonContext.Default.BotClearancePayload, out BotClearancePayload payload))
            return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (payload.IssuedAt > now + 60 || payload.ExpiresAt < now ||
            string.IsNullOrWhiteSpace(payload.Nonce))
            return false;

        if (!string.Equals(payload.Host, NormalizeHost(host), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(payload.RouteId, routeId, StringComparison.Ordinal) ||
            !string.Equals(payload.RoutePath, NormalizeCookiePath(routePath), StringComparison.Ordinal))
            return false;

        return !_options.BindClearanceToIp ||
               !string.IsNullOrWhiteSpace(payload.Ip) &&
               string.Equals(payload.Ip, clientIp, StringComparison.Ordinal);
    }

    public bool TryConsumeVerifyRequest(string? clientIp)
    {
        var key = string.IsNullOrWhiteSpace(clientIp) ? "unknown" : clientIp;
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var windowStart = now - now % 60;
        var limit = Math.Clamp(_options.VerifyRequestsPerMinute, 1, 300);

        while (true)
        {
            if (!_verifyRateWindows.TryGetValue(key, out var current) || current.WindowStart != windowStart)
            {
                if (_verifyRateWindows.TryUpdate(key, new VerifyRateWindow(windowStart, 1), current))
                    return true;
                if (current.WindowStart != windowStart && _verifyRateWindows.TryAdd(key, new VerifyRateWindow(windowStart, 1)))
                    return true;
                continue;
            }

            if (current.Count >= limit) return false;
            if (_verifyRateWindows.TryUpdate(key, new VerifyRateWindow(windowStart, current.Count + 1), current))
                return true;
        }
    }

    public void AppendClearanceCookie(HttpResponse response, string value, string routePath)
    {
        response.Cookies.Append(ClearanceCookieName, value, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            IsEssential = true,
            MaxAge = GetLifetime(),
            Path = NormalizeCookiePath(routePath)
        });
    }

    public async Task<BotVerificationResult> VerifyTurnstileAsync(
        string? token,
        string expectedAction,
        string requestHost,
        string? clientIp,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return new BotVerificationResult(BotVerificationStatus.Rejected);

        if (!IsTurnstileConfigured)
            return new BotVerificationResult(BotVerificationStatus.Misconfigured);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = _options.SecretKey,
                ["response"] = token,
                ["remoteip"] = clientIp ?? string.Empty
            });

            using var response = await client.PostAsync(_options.VerifyEndpoint, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new BotVerificationResult(BotVerificationStatus.Unavailable);

            var result = await response.Content.ReadFromJsonAsync(
                AppJsonContext.Default.TurnstileVerifyResponse,
                cancellationToken);
            if (result == null)
                return new BotVerificationResult(BotVerificationStatus.Unavailable);

            if (!result.Success || !string.Equals(result.Action, expectedAction, StringComparison.Ordinal))
                return new BotVerificationResult(BotVerificationStatus.Rejected);

            if (!IsAllowedHostname(result.Hostname, requestHost))
                return new BotVerificationResult(BotVerificationStatus.Rejected);

            return new BotVerificationResult(BotVerificationStatus.Success);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new BotVerificationResult(BotVerificationStatus.Unavailable);
        }
        catch (HttpRequestException)
        {
            return new BotVerificationResult(BotVerificationStatus.Unavailable);
        }
        catch (JsonException)
        {
            return new BotVerificationResult(BotVerificationStatus.Unavailable);
        }
    }

    private TimeSpan GetLifetime()
    {
        return TimeSpan.FromMinutes(Math.Clamp(_options.ClearanceLifetimeMinutes, 5, 1440));
    }

    private TimeSpan GetChallengeLifetime()
    {
        return TimeSpan.FromMinutes(Math.Clamp(_options.ChallengeLifetimeMinutes, 1, 30));
    }

    private bool TryGetSigningKey(out byte[] key)
    {
        key = Encoding.UTF8.GetBytes(_options.CookieSigningKey);
        if (key.Length >= 32) return true;

        key = [];
        return false;
    }

    private string Sign<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo)
    {
        return TryGetSigningKey(out var key) ? SignedToken.Sign(value, typeInfo, key) : string.Empty;
    }

    private bool TryReadSigned<T>(
        string? encoded,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo,
        out T value)
    {
        value = default!;
        return TryGetSigningKey(out var key) && SignedToken.TryRead(encoded, typeInfo, key, out value);
    }

    private bool IsAllowedHostname(string? responseHostname, string requestHost)
    {
        if (string.IsNullOrWhiteSpace(responseHostname)) return false;

        var hostname = NormalizeHost(responseHostname);
        var allowed = _options.AllowedHostnames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeHost)
            .ToArray();

        var requestHostname = NormalizeHost(requestHost);
        if (!string.Equals(hostname, requestHostname, StringComparison.OrdinalIgnoreCase))
            return false;

        if (allowed.Length == 0)
            return true;

        return allowed.Any(pattern =>
            pattern.StartsWith("*.", StringComparison.Ordinal)
                ? hostname.EndsWith(pattern[1..], StringComparison.OrdinalIgnoreCase)
                : string.Equals(hostname, pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeHost(string host)
    {
        host = host.Trim().TrimEnd('.');
        if (host.StartsWith("[", StringComparison.Ordinal))
        {
            var closing = host.IndexOf(']');
            return closing > 0 ? host[1..closing].ToLowerInvariant() : host.ToLowerInvariant();
        }

        var colon = host.LastIndexOf(':');
        if (colon > 0 && host.IndexOf(':') == colon) host = host[..colon];
        return host.ToLowerInvariant();
    }

    private static string NormalizeCookiePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/") return "/";
        path = path.Trim();
        return path.StartsWith('/') ? path : "/" + path;
    }

    private static bool IsSafeReturnUrl(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith("/", StringComparison.Ordinal) &&
               !value.StartsWith("//", StringComparison.Ordinal) &&
               !value.Contains('\\');
    }

    private readonly record struct VerifyRateWindow(long WindowStart, int Count);

    private void CleanupConsumedStates(long now)
    {
        foreach (var item in _consumedChallengeStates)
        {
            if (item.Value < now) _consumedChallengeStates.TryRemove(item.Key, out _);
        }
    }

    private static string CreateNonce()
    {
        return SignedToken.CreateNonce();
    }
}

public static class BotProtectionServiceExtensions
{
    public static IServiceCollection AddBotProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<BotProtectionOptions>(configuration.GetSection(BotProtectionOptions.Name));
        services.AddHttpClient(BotProtectionService.HttpClientName, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
        });
        services.AddSingleton<BotProtectionService>();
        return services;
    }
}
