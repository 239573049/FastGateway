using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Core.Entities;
using FastGateway.Gateway;
using FastGateway.Infrastructure;
using FastGateway.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace FastGateway.Tests;

public sealed class OriginPolicyTests
{
    [Theory]
    [InlineData("*.tree456.com", "https://office.tree456.com", true)]
    [InlineData("*.tree456.com", "http://a.b.tree456.com:8080", true)]
    [InlineData("*.tree456.com", "https://tree456.com", false)]
    [InlineData("*.tree456.com", "https://evil-tree456.com", false)]
    [InlineData("*.tree456.com", "https://tree456.com.evil.test", false)]
    [InlineData("https://*.tree456.com", "https://a.tree456.com:443", true)]
    [InlineData("https://*.tree456.com", "https://a.tree456.com:8443", false)]
    [InlineData("https://*.tree456.com:8443", "https://a.tree456.com:8443", true)]
    [InlineData("https://*.tree456.com:8443", "http://a.tree456.com:8443", false)]
    [InlineData("https://office.tree456.com", "https://OFFICE.tree456.com:443", true)]
    [InlineData("https://office.tree456.com", "https://office.tree456.com.evil.test", false)]
    [InlineData("https://office.tree456.com", "https://office.tree456.com/path", false)]
    [InlineData("*.tree456.com", "https://user@a.tree456.com", false)]
    [InlineData("*.tree456.com", "null", false)]
    [InlineData("*.example.org", "https://app.example.org", true)]
    [InlineData("*.example.org", "https://app.example.com", false)]
    [InlineData("https://*.example.co.uk:8443", "https://a.b.example.co.uk:8443", true)]
    [InlineData("https://*.example.co.uk:8443", "https://example.co.uk:8443", false)]
    public void IsAllowed_OriginMatchesRules_EnforcesHostSchemeAndPort(string rule, string origin, bool expected)
    {
        Assert.Equal(expected, new OriginPolicy([rule]).IsAllowed(origin));
    }

    [Theory]
    [InlineData("*")]
    [InlineData("tree456.com")]
    [InlineData("*.tree456.com:8080")]
    [InlineData("https://*tree456.com")]
    [InlineData("https://*.tree456.com/path")]
    [InlineData("https://*.tree456.com?x=1")]
    [InlineData("https://user@tree456.com")]
    [InlineData("https://tree456.com\nhttps://evil.test")]
    public void Constructor_InvalidRule_Rejects(string rule)
    {
        Assert.Throws<ValidationException>(() => new OriginPolicy([rule]));
    }

    [Fact]
    public async Task OriginSettings_PersistReloadAndApplyToCorsAndWebSockets()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fastgateway-origin-tests-" + Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(directory, "gateway.config");
        var config = new ConfigurationService(configPath);
        var fallback = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://legacy.example.com"
        }).Build();
        OriginPolicyStore.Initialize(config, fallback);

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddCors(options => options.AddPolicy("origins", policy => policy
            .SetIsOriginAllowed(OriginPolicyStore.IsAllowed).AllowAnyMethod().AllowAnyHeader().AllowCredentials()));
        builder.Services.AddSingleton(config);
        builder.Services.AddScoped<SettingProvide>();
        builder.Services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("test", _ => { });
        builder.Services.AddAuthorization();
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));
        var app = builder.Build();
        app.UseRouting();
        app.UseCors("origins");
        app.UseWebSockets();
        app.UseWebSocketOriginPolicy();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapGet("/", () => "ok");
        app.MapGet("/ws", async context =>
        {
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
        });
        app.MapSetting();

        try
        {
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new Uri(address) };
            Assert.False(OriginPolicyStore.Get().IsOverride);
            Assert.True(OriginPolicyStore.IsAllowed("https://legacy.example.com"));

            using (var unauthorized = await client.GetAsync("/api/v1/setting/cors-origins"))
                Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-Auth", "true");
            using (var get = await client.GetAsync("/api/v1/setting/cors-origins"))
            {
                using var body = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
                Assert.False(body.RootElement.GetProperty("data").GetProperty("isOverride").GetBoolean());
            }
            using (var update = await client.PutAsync("/api/v1/setting/cors-origins",
                       new StringContent("""{"origins":["*.tree456.com"]}""", Encoding.UTF8, "application/json")))
            {
                using var body = JsonDocument.Parse(await update.Content.ReadAsStringAsync());
                Assert.True(body.RootElement.GetProperty("success").GetBoolean());
            }
            Assert.Equal("*.tree456.com", new ConfigurationService(configPath).GetSetting(OriginPolicyStore.SettingKey)!.Value);
            Assert.True(OriginPolicyStore.Get().IsOverride);
            Assert.False(OriginPolicyStore.IsAllowed("https://legacy.example.com"));
            await AssertCors(client, "https://office.tree456.com", true);
            await AssertCors(client, "https://evil-tree456.com", false);
            await AssertWebSocket(client, "https://office.tree456.com", HttpStatusCode.OK);
            await AssertWebSocket(client, "https://evil-tree456.com", HttpStatusCode.Forbidden);
            await AssertWebSocket(client, null, HttpStatusCode.OK);
            using (var socket = new ClientWebSocket())
            {
                socket.Options.SetRequestHeader("Origin", "https://office.tree456.com");
                var wsAddress = new UriBuilder(address) { Scheme = "ws", Path = "/ws" }.Uri;
                await socket.ConnectAsync(wsAddress, CancellationToken.None);
                Assert.True(socket.State is WebSocketState.Open or WebSocketState.CloseReceived);
            }

            Assert.Throws<ValidationException>(() => OriginPolicyStore.Save(config, ["*"]));
            Assert.True(OriginPolicyStore.IsAllowed("https://office.tree456.com"));
            using (var invalid = await client.PutAsync("/api/v1/setting/cors-origins",
                       new StringContent("""{"origins":["*"]}""", Encoding.UTF8, "application/json")))
            {
                using var body = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
                Assert.False(body.RootElement.GetProperty("success").GetBoolean());
            }
            Directory.CreateDirectory(configPath + ".tmp");
            try
            {
                var error = Record.Exception(() => OriginPolicyStore.Save(config, ["https://other.example.com"]));
                Assert.True(error is IOException or UnauthorizedAccessException);
                Assert.Equal("*.tree456.com", config.GetSetting(OriginPolicyStore.SettingKey)!.Value);
                Assert.True(OriginPolicyStore.IsAllowed("https://office.tree456.com"));
            }
            finally
            {
                Directory.Delete(configPath + ".tmp");
            }
            OriginPolicyStore.Save(config, []);
            Assert.False(OriginPolicyStore.IsAllowed("https://office.tree456.com"));
            Assert.True(OriginPolicyStore.Get().IsOverride);
            config.ReplaceConfig(new ConfigurationService(configPath).ExportSnapshot());
            Assert.True(OriginPolicyStore.Get().IsOverride);
            Assert.False(OriginPolicyStore.IsAllowed("https://legacy.example.com"));
            config.ReplaceConfig(new GatewayConfig
            {
                Settings = [new Setting { Key = OriginPolicyStore.SettingKey, Value = "*" }]
            });
            Assert.False(OriginPolicyStore.IsAllowed("https://legacy.example.com"));
            config.ReplaceConfig(new GatewayConfig());
            Assert.True(OriginPolicyStore.IsAllowed("https://legacy.example.com"));
            OriginPolicyStore.Save(config, ["https://other.example.com"]);
            using (var reset = await client.DeleteAsync("/api/v1/setting/cors-origins"))
            {
                using var body = JsonDocument.Parse(await reset.Content.ReadAsStringAsync());
                Assert.True(body.RootElement.GetProperty("success").GetBoolean());
            }
            Assert.False(OriginPolicyStore.Get().IsOverride);
            Assert.True(OriginPolicyStore.IsAllowed("https://legacy.example.com"));
        }

        finally
        {
            await app.DisposeAsync();
            if (File.Exists(configPath)) File.Delete(configPath);
            if (Directory.Exists(directory)) Directory.Delete(directory);
        }
    }

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.ContainsKey("X-Test-Auth"))
                return Task.FromResult(AuthenticateResult.NoResult());
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "test")], Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
        }
    }

    private static async Task AssertCors(HttpClient client, string origin, bool allowed)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        if (allowed)
        {
            Assert.Equal(origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
            Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        }
    }

    private static async Task AssertWebSocket(HttpClient client, string? origin, HttpStatusCode expected)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Connection.Add("Upgrade");
        request.Headers.Upgrade.Add(new ProductHeaderValue("websocket"));
        request.Headers.Add("Sec-WebSocket-Version", "13");
        request.Headers.Add("Sec-WebSocket-Key", "dGhlIHNhbXBsZSBub25jZQ==");
        if (origin != null) request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request);
        Assert.Equal(expected, response.StatusCode);
    }
}
