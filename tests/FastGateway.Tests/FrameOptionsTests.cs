using System.Text.Json;
using Core.Entities;
using FastGateway.Cluster;
using FastGateway.Infrastructure;
using FastGateway.Services;
using Xunit;

namespace FastGateway.Tests;

public sealed class FrameOptionsTests
{
    [Theory]
    [InlineData(null, "SAMEORIGIN")]
    [InlineData("", "SAMEORIGIN")]
    [InlineData("   ", "SAMEORIGIN")]
    [InlineData("sameorigin", "SAMEORIGIN")]
    [InlineData(" SAMEORIGIN ", "SAMEORIGIN")]
    [InlineData("DENY", "DENY")]
    [InlineData("deny", "DENY")]
    [InlineData("garbage", "SAMEORIGIN")]
    public void ResolveFrameOptions_NoUpstreamPolicy_UsesConfiguredOrDefault(string? configured, string expected)
    {
        Assert.Equal(expected, Gateway.Gateway.ResolveFrameOptions(configured, false));
    }

    [Theory]
    [InlineData("NONE")]
    [InlineData("none")]
    [InlineData("OFF")]
    [InlineData("DISABLE")]
    [InlineData("DISABLED")]
    public void ResolveFrameOptions_Disabled_DoesNotInject(string configured)
    {
        Assert.Null(Gateway.Gateway.ResolveFrameOptions(configured, false));
    }

    [Fact]
    public void ResolveFrameOptions_UpstreamDeclaresFramePolicy_DoesNotInject()
    {
        Assert.Null(Gateway.Gateway.ResolveFrameOptions("DENY", true));
        Assert.Null(Gateway.Gateway.ResolveFrameOptions(null, true));
        Assert.Null(Gateway.Gateway.ResolveFrameOptions("SAMEORIGIN", true));
    }

    [Fact]
    public void GatewayConfig_FrameOptions_RoundTrips()
    {
        var config = new GatewayConfig();
        config.Servers.Add(new Server
        {
            Id = "s1", Name = "https", Listen = 443, IsHttps = true, FrameOptions = "DENY"
        });

        var json = JsonSerializer.Serialize(config, ConfigJsonContext.Default.GatewayConfig);
        var restored = JsonSerializer.Deserialize(json, ConfigJsonContext.Default.GatewayConfig)!;

        Assert.Equal("DENY", Assert.Single(restored.Servers).FrameOptions);
    }

    [Fact]
    public void GatewayConfig_LegacyFileWithoutField_FallsBackToSameOrigin()
    {
        // 2.14.0 及更早的 gateway.config 里没有 frameOptions 字段
        const string legacy = """{"servers":[{"id":"s1","listen":443,"name":"old","isHttps":true}],"domainNames":[]}""";
        var config = JsonSerializer.Deserialize(legacy, ConfigJsonContext.Default.GatewayConfig)!;
        var server = Assert.Single(config.Servers);

        Assert.Null(server.FrameOptions);
        Assert.Equal("SAMEORIGIN", Gateway.Gateway.ResolveFrameOptions(server.FrameOptions, false));
    }

    [Fact]
    public void ClusterConfig_FrameOptions_RoundTripsToWorker()
    {
        var config = new GatewayConfig();
        config.Servers.Add(new Server
        {
            Id = "s1", Name = "https", Listen = 443, IsHttps = true, FrameOptions = "NONE"
        });

        var frame = ClusterProtocol.Encode(new ClusterMessage
        {
            Type = ClusterMessage.TypeConfig,
            Config = new ClusterConfigPayload { Config = config }
        });
        var stream = new MemoryStream();
        stream.Write(frame);
        var decoded = ClusterProtocol.Decode(stream)!.Config!.Config;

        Assert.Equal("NONE", Assert.Single(decoded.Servers).FrameOptions);
    }
}
