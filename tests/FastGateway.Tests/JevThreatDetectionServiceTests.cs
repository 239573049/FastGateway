using FastGateway.Services;
using Xunit;

namespace FastGateway.Tests;

public sealed class JevThreatDetectionServiceTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/assets/index-abc123.js")]
    [InlineData("/static/app.css")]
    [InlineData("/images/photo.webp")]
    [InlineData("/video/demo.mp4")]
    [InlineData("/docs/manual.pdf")]
    [InlineData("/favicon.ico")]
    [InlineData("/robots.txt")]
    public void ShouldIgnoreForJev_BenignPageAndStaticResources_ReturnsTrue(string path)
    {
        Assert.True(JevThreatDetectionService.ShouldIgnoreForJev(path));
    }

    [Theory]
    [InlineData("/.env")]
    [InlineData("/.git/config")]
    [InlineData("/api/auth/login")]
    [InlineData("/admin/config.php")]
    [InlineData("/mcp")]
    [InlineData("/appsettings.json")]
    public void ShouldIgnoreForJev_SecurityRelevantPaths_ReturnsFalse(string path)
    {
        Assert.False(JevThreatDetectionService.ShouldIgnoreForJev(path));
    }
}
