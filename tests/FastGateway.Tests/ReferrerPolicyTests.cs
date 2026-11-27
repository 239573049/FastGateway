using System.Text.Json;
using Core.Entities;
using FastGateway.Cluster;
using FastGateway.Infrastructure;
using FastGateway.Services;
using Xunit;

namespace FastGateway.Tests;

public sealed class ReferrerPolicyTests
{
    [Theory]
    [InlineData(null, "no-referrer")]
    [InlineData("", "no-referrer")]
    [InlineData("   ", "no-referrer")]
    [InlineData("no-referrer", "no-referrer")]
    [InlineData("STRICT-ORIGIN-WHEN-CROSS-ORIGIN", "strict-origin-when-cross-origin")]
    [InlineData(" unsafe-url ", "unsafe-url")]
    [InlineData("garbage", "no-referrer")]
    [InlineData("no-referrer\r\nX-Evil: 1", "no-referrer")]
    public void ResolveReferrerPolicy_NoUpstreamPolicy_UsesConfiguredOrDefault(string? configured, string expected)
    {
        Assert.Equal(expected, Gateway.Gateway.ResolveReferrerPolicy(configured, false));
    }

    [Theory]
    [InlineData("NONE")]
    [InlineData("none")]
    [InlineData("OFF")]
    [InlineData("DISABLE")]
    [InlineData("DISABLED")]
    public void ResolveReferrerPolicy_Disabled_DoesNotInject(string configured)
    {
        Assert.Null(Gateway.Gateway.ResolveReferrerPolicy(configured, false));
    }

    [Fact]
    public void ResolveReferrerPolicy_UpstreamDeclaresPolicy_DoesNotInject()
    {
        Assert.Null(Gateway.Gateway.ResolveReferrerPolicy("strict-origin-when-cross-origin", true));
        Assert.Null(Gateway.Gateway.ResolveReferrerPolicy(null, true));
    }

    [Fact]
    public void GatewayConfig_ReferrerPolicy_RoundTrips()
    {
        var config = new GatewayConfig();
        config.Servers.Add(new Server
        {
            Id = "s1", Name = "https", Listen = 443, IsHttps = true, ReferrerPolicy = "NONE"
        });

        var json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.GatewayConfig);
        var restored = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.GatewayConfig)!;

        Assert.Equal("NONE", Assert.Single(restored.Servers).ReferrerPolicy);
    }

    [Fact]
    public void GatewayConfig_LegacyFileWithoutField_FallsBackToNoReferrer()
    {
        // 旧 gateway.config 里没有 referrerPolicy 字段，行为需与引入该开关前一致
        const string legacy = """{"servers":[{"id":"s1","listen":443,"name":"old","isHttps":true}],"domainNames":[]}""";
        var config = JsonSerializer.Deserialize(legacy, ConfigJsonContext.Default.GatewayConfig)!;
        var server = Assert.Single(config.Servers);

        Assert.Null(server.ReferrerPolicy);
        Assert.Equal("no-referrer", Gateway.Gateway.ResolveReferrerPolicy(server.ReferrerPolicy, false));
    }

    [Fact]
    public void ClusterConfig_ReferrerPolicy_RoundTripsToWorker()
    {
        var config = new GatewayConfig();
        config.Servers.Add(new Server
        {
            Id = "s1", Name = "https", Listen = 443, IsHttps = true, ReferrerPolicy = "strict-origin-when-cross-origin"
        });

        var frame = ClusterProtocol.Encode(new ClusterMessage
        {
            Type = ClusterMessage.TypeConfig,
            Config = new ClusterConfigPayload { Config = config }
        });
        var stream = new MemoryStream();
        stream.Write(frame);
        var decoded = ClusterProtocol.Decode(stream)!.Config!.Config;

        Assert.Equal("strict-origin-when-cross-origin", Assert.Single(decoded.Servers).ReferrerPolicy);
    }
}
