using FastGateway.Security;
using Xunit;

namespace FastGateway.Tests;

public sealed class AdaptiveThreatDetectorTests
{
    [Fact]
    public void InspectRequest_SensitiveEnvironmentFile_BlocksImmediately()
    {
        const string ip = "203.0.113.10";
        AdaptiveThreatStore.Release(ip);

        var profile = AdaptiveThreatDetector.InspectRequest(ip, "/.env", "GET", "curl/8.0");

        Assert.True(profile.ShouldTrack);
        Assert.True(profile.BlockNow);
        Assert.Contains("env", profile.Reason);
        Assert.True(AdaptiveThreatStore.IsBlocked(ip));
        AdaptiveThreatStore.Release(ip);
    }

    [Fact]
    public void InspectRequest_PathTraversal_BlocksImmediately()
    {
        const string ip = "203.0.113.11";
        AdaptiveThreatStore.Release(ip);

        var profile = AdaptiveThreatDetector.InspectRequest(ip, "/..%2f..%2fetc/passwd", "GET", "scan");

        Assert.True(profile.BlockNow);
        Assert.Contains("穿越", profile.Reason);
        AdaptiveThreatStore.Release(ip);
    }

    [Fact]
    public void InspectRequest_PrivateAndLoopbackIps_AreIgnored()
    {
        Assert.False(AdaptiveThreatDetector.ShouldTrackIp("127.0.0.1"));
        Assert.False(AdaptiveThreatDetector.ShouldTrackIp("10.0.0.8"));
        Assert.False(AdaptiveThreatDetector.ShouldTrackIp("172.18.0.1"));
        Assert.False(AdaptiveThreatDetector.ShouldTrackIp("192.168.1.2"));
        Assert.True(AdaptiveThreatDetector.ShouldTrackIp("8.8.8.8"));
    }

    [Fact]
    public void ObserveResponse_SingleNormal404_DoesNotQuarantine()
    {
        const string ip = "203.0.113.12";
        AdaptiveThreatStore.Release(ip);
        var profile = AdaptiveThreatDetector.InspectRequest(ip, "/missing-page", "GET", BrowserUserAgent);

        var quarantine = AdaptiveThreatDetector.ObserveResponse(profile, 404, "test");

        Assert.Null(quarantine);
        Assert.False(AdaptiveThreatStore.IsBlocked(ip));
    }

    [Fact]
    public void ObserveResponse_AdminScanner_QuarantinesAfterSuspicious404()
    {
        const string ip = "203.0.113.13";
        AdaptiveThreatStore.Release(ip);

        var first = AdaptiveThreatDetector.InspectRequest(ip, "/manager/html", "GET", BrowserUserAgent);
        Assert.False(first.BlockNow);
        var quarantine = AdaptiveThreatDetector.ObserveResponse(first, 404, "test");

        Assert.NotNull(quarantine);
        Assert.True(quarantine.Score >= 60);
        Assert.True(AdaptiveThreatStore.IsBlocked(ip));
        AdaptiveThreatStore.Release(ip);
    }

    [Fact]
    public void ObserveResponse_ManyDistinct404s_Quarantines()
    {
        const string ip = "203.0.113.14";
        AdaptiveThreatStore.Release(ip);

        for (var i = 0; i < 30; i++)
        {
            var profile = AdaptiveThreatDetector.InspectRequest(ip, $"/missing-{i}", "GET", BrowserUserAgent);
            AdaptiveThreatDetector.ObserveResponse(profile, 404, "test");
        }

        Assert.True(AdaptiveThreatStore.IsBlocked(ip));
        AdaptiveThreatStore.Release(ip);
    }

    [Fact]
    public void InspectRequest_NormalApiTraffic_IsAllowed()
    {
        const string ip = "203.0.113.15";
        AdaptiveThreatStore.Release(ip);

        var profile = AdaptiveThreatDetector.InspectRequest(ip, "/api/v1/statistics/overview", "GET", BrowserUserAgent);

        Assert.False(profile.BlockNow);
        Assert.Equal(0, profile.SignatureWeight);
        Assert.False(AdaptiveThreatStore.IsBlocked(ip));
    }

    private const string BrowserUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/140.0 Safari/537.36";
}
