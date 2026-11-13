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
}
