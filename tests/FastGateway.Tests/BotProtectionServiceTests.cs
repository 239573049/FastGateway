using System.Net;
using System.Net.Http.Json;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Options;
using FastGateway.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace FastGateway.Tests;

public sealed class BotProtectionServiceTests
{
    [Fact]
    public void CreateClearance_ValidatesHostRoutePathAndSignature()
    {
        var service = CreateService();
        var clearance = service.CreateClearance("Example.com:443", "route-a", "docs", "203.0.113.10");

        Assert.True(service.ValidateClearance(clearance, "example.com", "route-a", "/docs", "203.0.113.10"));
        Assert.False(service.ValidateClearance(clearance, "other.example.com", "route-a", "/docs", "203.0.113.10"));
        Assert.False(service.ValidateClearance(clearance, "example.com", "route-b", "/docs", "203.0.113.10"));
        Assert.False(service.ValidateClearance(clearance, "example.com", "route-a", "/other", "203.0.113.10"));
        Assert.False(service.ValidateClearance(Tamper(clearance), "example.com", "route-a", "/docs", "203.0.113.10"));
    }

    [Fact]
    public void CreateClearance_WhenIpBindingEnabled_RejectsDifferentIp()
    {
        var service = CreateService(options: new BotProtectionOptions { BindClearanceToIp = true });
        var clearance = service.CreateClearance("example.com", "route-a", "/", "203.0.113.10");

        Assert.True(service.ValidateClearance(clearance, "example.com", "route-a", "/", "203.0.113.10"));
        Assert.False(service.ValidateClearance(clearance, "example.com", "route-a", "/", "203.0.113.11"));
    }

    [Fact]
    public void CreateChallengeState_RejectsUnsafeReturnUrlAndConsumesOnce()
    {
        var service = CreateService();
        var unsafeState = service.CreateChallengeState("example.com", "route-a", "https://evil.example/", "/", "business-access");
        Assert.False(service.TryReadChallengeState(unsafeState, "example.com", out _));

        var state = service.CreateChallengeState("example.com", "route-a", "/docs?q=1", "docs", "business-access");
        Assert.True(service.TryReadChallengeState(state, "example.com", out var payload));
        Assert.Equal("/docs?q=1", payload.ReturnUrl);
        Assert.Equal("/docs", payload.RoutePath);
        Assert.True(service.TryConsumeChallengeState(state, "example.com", out _));
        Assert.False(service.TryConsumeChallengeState(state, "example.com", out _));
        Assert.False(service.TryReadChallengeState(state, "other.example.com", out _));
    }

    [Fact]
    public void ValidateClearance_WithChallengeState_Rejects()
    {
        var service = CreateService();
        var state = service.CreateChallengeState("example.com", "route-a", "/", "/", "business-access");

        Assert.False(service.ValidateClearance(state, "example.com", "route-a", "/", "203.0.113.10"));
    }

    [Fact]
    public void CreateRouteMetadata_DefaultsOffAndPreservesExistingMetadata()
    {
        var existing = new Dictionary<string, string> { ["existing"] = "value" };
        var unprotected = new Core.Entities.DomainName { Id = "route-a", EnableBotProtection = false };
        var protectedRoute = new Core.Entities.DomainName { Id = "route-a", EnableBotProtection = true };

        var withoutProtection = BotProtectionService.CreateRouteMetadata(unprotected, "/");
        var withProtection = BotProtectionService.CreateRouteMetadata(protectedRoute, "/docs", existing);

        Assert.Empty(withoutProtection);
        Assert.Equal("value", withProtection["existing"]);
        Assert.Equal("true", withProtection[BotProtectionService.ProtectionMetadataKey]);
        Assert.Equal("route-a", withProtection[BotProtectionService.RouteIdMetadataKey]);
        Assert.Equal("/docs", withProtection[BotProtectionService.RoutePathMetadataKey]);
    }

    [Fact]
    public void TryConsumeVerifyRequest_EnforcesPerIpWindow()
    {
        var service = CreateService(options: new BotProtectionOptions { VerifyRequestsPerMinute = 2 });

        Assert.True(service.TryConsumeVerifyRequest("203.0.113.10"));
        Assert.True(service.TryConsumeVerifyRequest("203.0.113.10"));
        Assert.False(service.TryConsumeVerifyRequest("203.0.113.10"));
        Assert.True(service.TryConsumeVerifyRequest("203.0.113.11"));
    }

    [Fact]
    public async Task VerifyTurnstileAsync_MapsSuccessAndSendsServerOnlySecret()
    {
        var handler = new StubHandler(_ => JsonResponse(new TurnstileVerifyResponse
        {
            Success = true,
            Action = "business-access",
            Hostname = "example.com"
        }));
        var service = CreateService(handler);

        var result = await service.VerifyTurnstileAsync(
            "turnstile-token", "business-access", "example.com", "203.0.113.10", CancellationToken.None);

        Assert.Equal(BotVerificationStatus.Success, result.Status);
        Assert.NotNull(handler.LastRequest);
        Assert.Contains("secret=server-secret", handler.LastRequestBody);
        Assert.Contains("response=turnstile-token", handler.LastRequestBody);
        Assert.Contains("remoteip=203.0.113.10", handler.LastRequestBody);
        Assert.Equal("https://challenges.cloudflare.com/turnstile/v0/siteverify", handler.LastRequest.RequestUri!.ToString());
    }

    [Fact]
    public async Task VerifyTurnstileAsync_RejectsActionAndHostnameMismatch()
    {
        var actionHandler = new StubHandler(_ => JsonResponse(new TurnstileVerifyResponse
        {
            Success = true,
            Action = "other-action",
            Hostname = "example.com"
        }));
        var actionResult = await CreateService(actionHandler).VerifyTurnstileAsync(
            "token", "business-access", "example.com", null, CancellationToken.None);
        Assert.Equal(BotVerificationStatus.Rejected, actionResult.Status);

        var hostnameHandler = new StubHandler(_ => JsonResponse(new TurnstileVerifyResponse
        {
            Success = true,
            Action = "business-access",
            Hostname = "other.example.com"
        }));
        var hostnameResult = await CreateService(hostnameHandler).VerifyTurnstileAsync(
            "token", "business-access", "example.com", null, CancellationToken.None);
        Assert.Equal(BotVerificationStatus.Rejected, hostnameResult.Status);
    }

    [Fact]
    public async Task VerifyTurnstileAsync_MapsHttpFailureAndTimeoutToUnavailable()
    {
        var failureHandler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        var failureResult = await CreateService(failureHandler).VerifyTurnstileAsync(
            "token", "business-access", "example.com", null, CancellationToken.None);
        Assert.Equal(BotVerificationStatus.Unavailable, failureResult.Status);

        var timeoutHandler = new StubHandler(_ => throw new HttpRequestException("network failure"));
        var timeoutResult = await CreateService(timeoutHandler).VerifyTurnstileAsync(
            "token", "business-access", "example.com", null, CancellationToken.None);
        Assert.Equal(BotVerificationStatus.Unavailable, timeoutResult.Status);
    }

    private static BotProtectionService CreateService(
        HttpMessageHandler? handler = null,
        BotProtectionOptions? options = null)
    {
        options ??= new BotProtectionOptions();
        options.SiteKey = "site-key";
        options.SecretKey = "server-secret";
        options.CookieSigningKey = "01234567890123456789012345678901";

        var client = new HttpClient(handler ?? new StubHandler(_ => JsonResponse(new TurnstileVerifyResponse())))
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        return new BotProtectionService(new StubHttpClientFactory(client), Microsoft.Extensions.Options.Options.Create(options));
    }

    private static HttpResponseMessage JsonResponse(TurnstileVerifyResponse response)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(response, AppJsonContext.Default.TurnstileVerifyResponse)
        };
    }

    private static string Tamper(string value)
    {
        var separator = value.IndexOf('.');
        var signature = value[(separator + 1)..];
        var replacement = signature[0] == 'A' ? 'B' : 'A';
        return value[..(separator + 1)] + replacement + signature[1..];
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastRequestBody = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return handler(request);
        }
    }
}
