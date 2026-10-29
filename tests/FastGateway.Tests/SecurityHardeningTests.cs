using FastGateway.Cluster;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using Xunit;

namespace FastGateway.Tests;

public sealed class SecurityHardeningTests
{
    [Theory]
    [InlineData("/index.html")]
    [InlineData("/assets/app.js")]
    [InlineData("/")]
    public void TryResolveStaticFile_PathInsideRoot_Resolves(string requestPath)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fg-static-root"));

        Assert.True(Gateway.Gateway.TryResolveStaticFile(root, requestPath, out var resolved));
        Assert.StartsWith(root, resolved);
    }

    [Theory]
    [InlineData("//etc/passwd")]
    [InlineData("/../secret.txt")]
    [InlineData("/assets/../../secret.txt")]
    [InlineData("/C:/Windows/win.ini")]
    [InlineData("/..\\..\\secret.txt")]
    public void TryResolveStaticFile_PathEscapingRoot_Rejects(string requestPath)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "fg-static-root"));

        if (Gateway.Gateway.TryResolveStaticFile(root, requestPath, out var resolved))
            Assert.StartsWith(root + Path.DirectorySeparatorChar, resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    [InlineData("/api/v1")]
    [InlineData("/app-1/static_files.v2/~user")]
    public void IsSafeTunnelRoute_PlainPath_Accepts(string? route)
    {
        Assert.True(Gateway.Gateway.IsSafeTunnelRoute(route));
    }

    [Theory]
    [InlineData("@169.254.169.254/latest")]
    [InlineData("/@169.254.169.254")]
    [InlineData("//evil.example")]
    [InlineData("/a/../b")]
    [InlineData("/a?b=1")]
    [InlineData("/a#b")]
    [InlineData("/a\\b")]
    [InlineData("api")]
    public void IsSafeTunnelRoute_UrlChangingRoute_Rejects(string route)
    {
        Assert.False(Gateway.Gateway.IsSafeTunnelRoute(route));
    }

    [Fact]
    public void SignedTokenTryRead_TokenSignedForOtherType_Rejects()
    {
        var key = new byte[32];
        var state = SignedToken.Sign(new BotChallengeState
        {
            Host = "example.com", RouteId = "route-a", RoutePath = "/", Nonce = "n",
            IssuedAt = 1, ExpiresAt = long.MaxValue
        }, AppJsonContext.Default.BotChallengeState, key);

        Assert.True(SignedToken.TryRead(state, AppJsonContext.Default.BotChallengeState, key, out _));
        Assert.False(SignedToken.TryRead(state, AppJsonContext.Default.BotClearancePayload, key, out _));
    }

    [Theory]
    [InlineData("abc", "abc", true)]
    [InlineData("abc", "abd", false)]
    [InlineData("abc", null, false)]
    [InlineData("", "", false)]
    public void ClusterTokenEquals_ComparesExactly(string? expected, string? actual, bool result)
    {
        Assert.Equal(result, ClusterStateService.TokenEquals(expected, actual));
    }
}
