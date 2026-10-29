using FastGateway.Risk;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FastGateway.Tests;

public sealed class AdminLoginGuardTests
{
    [Fact]
    public void RecordFailure_BelowDelayThreshold_NoDelay()
    {
        var guard = CreateGuard(out _);

        for (var i = 1; i < AdminLoginGuard.DelayThreshold; i++)
        {
            var failure = guard.RecordFailure("203.0.113.10");
            Assert.Equal(i, failure.RecentFailures);
            Assert.Equal(TimeSpan.Zero, failure.Delay);
        }
    }

    [Fact]
    public void GetDelay_AfterDelayThreshold_DoublesAndCaps()
    {
        Assert.Equal(TimeSpan.FromMilliseconds(200), AdminLoginGuard.GetDelay(AdminLoginGuard.DelayThreshold));
        Assert.Equal(TimeSpan.FromMilliseconds(400), AdminLoginGuard.GetDelay(AdminLoginGuard.DelayThreshold + 1));
        Assert.Equal(TimeSpan.FromSeconds(3), AdminLoginGuard.GetDelay(AdminLoginGuard.IpLockThreshold));
        Assert.Equal(TimeSpan.FromSeconds(3), AdminLoginGuard.GetDelay(1000));
    }

    [Fact]
    public void RecordFailure_ReachingIpThreshold_LocksOnlyThatIp()
    {
        var guard = CreateGuard(out _);

        for (var i = 1; i < AdminLoginGuard.IpLockThreshold; i++)
            Assert.Null(guard.RecordFailure("203.0.113.10").LockedFor);

        var last = guard.RecordFailure("203.0.113.10");

        Assert.Equal(AdminLoginGuard.LockDuration, last.LockedFor);
        Assert.Equal(AdminLoginGuard.LockDuration, guard.GetLockRemaining("203.0.113.10"));
        Assert.Null(guard.GetLockRemaining("203.0.113.11"));
        Assert.Null(guard.GetLockRemaining("198.51.100.10"));
    }

    [Fact]
    public void GetLockRemaining_AfterLockDuration_Unlocks()
    {
        var guard = CreateGuard(out var time);
        for (var i = 0; i < AdminLoginGuard.IpLockThreshold; i++) guard.RecordFailure("203.0.113.10");

        time.Advance(AdminLoginGuard.LockDuration - TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromSeconds(1), guard.GetLockRemaining("203.0.113.10"));

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(guard.GetLockRemaining("203.0.113.10"));

        // 解锁时旧失败已滑出窗口，下一次失败重新从 1 开始计数
        var next = guard.RecordFailure("203.0.113.10");
        Assert.Equal(1, next.RecentFailures);
        Assert.Null(next.LockedFor);
    }

    [Fact]
    public void RecordFailure_OutsideWindow_DoesNotAccumulate()
    {
        var guard = CreateGuard(out var time);

        for (var i = 0; i < AdminLoginGuard.IpLockThreshold - 1; i++) guard.RecordFailure("203.0.113.10");
        time.Advance(AdminLoginGuard.Window);

        var failure = guard.RecordFailure("203.0.113.10");
        Assert.Equal(1, failure.RecentFailures);
        Assert.Null(failure.LockedFor);
        Assert.Null(guard.GetLockRemaining("203.0.113.10"));
    }

    [Fact]
    public void RecordFailure_ReachingSubnetThreshold_LocksWholeSubnet()
    {
        var guard = CreateGuard(out _);

        // 每个 IP 只失败一次，单 IP 永远达不到阈值，只有子网维度能发现
        AdminLoginFailure last = default;
        for (var i = 1; i <= AdminLoginGuard.SubnetLockThreshold; i++)
            last = guard.RecordFailure($"203.0.113.{i}");

        Assert.Equal(1, last.RecentFailures);
        Assert.Equal(AdminLoginGuard.LockDuration, last.LockedFor);
        Assert.NotNull(guard.GetLockRemaining("203.0.113.200"));
        Assert.Null(guard.GetLockRemaining("203.0.114.1"));
    }

    [Fact]
    public void GetRecentFailures_CountsOnlyWindowForThatIp()
    {
        var guard = CreateGuard(out var time);
        for (var i = 0; i < 3; i++) guard.RecordFailure("203.0.113.10");

        Assert.Equal(3, guard.GetRecentFailures("203.0.113.10"));
        Assert.Equal(0, guard.GetRecentFailures("203.0.113.11"));

        time.Advance(AdminLoginGuard.Window);
        Assert.Equal(0, guard.GetRecentFailures("203.0.113.10"));
    }

    [Fact]
    public void RecordSuccess_AfterFailures_ClearsIpCount()
    {
        var guard = CreateGuard(out _);
        for (var i = 0; i < AdminLoginGuard.DelayThreshold; i++) guard.RecordFailure("203.0.113.10");

        guard.RecordSuccess("203.0.113.10");

        Assert.Equal(1, guard.RecordFailure("203.0.113.10").RecentFailures);
    }

    [Fact]
    public void RecordFailure_MissingIp_TrackedUnderSharedKey()
    {
        var guard = CreateGuard(out _);
        for (var i = 0; i < AdminLoginGuard.IpLockThreshold; i++) guard.RecordFailure(null);

        Assert.NotNull(guard.GetLockRemaining(string.Empty));
        Assert.Null(guard.GetLockRemaining("203.0.113.10"));
    }

    [Fact]
    public void GetActiveLocks_LockedIpAndSubnet_ListsBothUntilUnlocked()
    {
        var guard = CreateGuard(out var time);
        for (var i = 0; i < AdminLoginGuard.IpLockThreshold; i++) guard.RecordFailure("198.51.100.10");
        for (var i = 1; i <= AdminLoginGuard.SubnetLockThreshold; i++) guard.RecordFailure($"203.0.113.{i}");

        var locks = guard.GetActiveLocks();

        Assert.Contains(locks, x => x is { Subject: "198.51.100.10", IsSubnet: false });
        var subnetLock = Assert.Single(locks, x => x.IsSubnet);
        Assert.Equal("203.0.113.0/24", subnetLock.Subject);
        Assert.Equal(AdminLoginGuard.LockDuration, subnetLock.LockedUntil - subnetLock.LockedAt);

        Assert.True(guard.Unlock("203.0.113.0/24", isSubnet: true));
        Assert.True(guard.Unlock("198.51.100.10", isSubnet: false));
        Assert.False(guard.Unlock("198.51.100.10", isSubnet: false));
        Assert.Null(guard.GetLockRemaining("203.0.113.1"));
        Assert.Null(guard.GetLockRemaining("198.51.100.10"));

        time.Advance(AdminLoginGuard.LockDuration);
        Assert.Empty(guard.GetActiveLocks());
    }

    [Fact]
    public void RecordFailure_RepeatedLocksWithinHistory_EscalatesDuration()
    {
        var guard = CreateGuard(out var time);

        foreach (var expected in AdminLoginGuard.LockDurations.Append(AdminLoginGuard.LockDurations[^1]))
        {
            AdminLoginFailure last = default;
            for (var i = 0; i < AdminLoginGuard.IpLockThreshold; i++) last = guard.RecordFailure("203.0.113.10");

            Assert.Equal(expected, last.LockedFor);
            Assert.Equal(expected, guard.GetLockRemaining("203.0.113.10"));
            time.Advance(expected);
        }
    }

    [Fact]
    public void RecordFailure_LockAfterHistoryWindow_ResetsToFirstLevel()
    {
        var guard = CreateGuard(out var time);
        for (var i = 0; i < AdminLoginGuard.IpLockThreshold; i++) guard.RecordFailure("203.0.113.10");

        // 历史从解锁时刻起算
        time.Advance(AdminLoginGuard.LockDuration + AdminLoginGuard.LockHistoryWindow);

        AdminLoginFailure last = default;
        for (var i = 0; i < AdminLoginGuard.IpLockThreshold; i++) last = guard.RecordFailure("203.0.113.10");
        Assert.Equal(AdminLoginGuard.LockDuration, last.LockedFor);
    }

    [Fact]
    public void Unlock_ClearsEscalationHistory()
    {
        var guard = CreateGuard(out _);
        for (var i = 0; i < AdminLoginGuard.IpLockThreshold; i++) guard.RecordFailure("203.0.113.10");

        guard.Unlock("203.0.113.10", isSubnet: false);

        AdminLoginFailure last = default;
        for (var i = 0; i < AdminLoginGuard.IpLockThreshold; i++) last = guard.RecordFailure("203.0.113.10");
        Assert.Equal(AdminLoginGuard.LockDuration, last.LockedFor);
    }

    [Fact]
    public void GetActiveLocks_EscalatedLock_ReportsActualPeriod()
    {
        var guard = CreateGuard(out var time);
        for (var i = 0; i < AdminLoginGuard.IpLockThreshold; i++) guard.RecordFailure("203.0.113.10");
        time.Advance(AdminLoginGuard.LockDuration);
        for (var i = 0; i < AdminLoginGuard.IpLockThreshold; i++) guard.RecordFailure("203.0.113.10");

        var ipLock = Assert.Single(guard.GetActiveLocks(), x => !x.IsSubnet);
        Assert.Equal(AdminLoginGuard.LockDurations[1], ipLock.LockedUntil - ipLock.LockedAt);
    }

    [Theory]
    [InlineData("203.0.113.57", "203.0.113.0/24")]
    [InlineData("::ffff:203.0.113.57", "203.0.113.0/24")]
    [InlineData("2001:db8:1:2:3:4:5:6", "2001:db8:1:2::/64")]
    [InlineData("not-an-ip", null)]
    [InlineData("", null)]
    public void GetSubnetKey_VariousInputs_ReturnsNetworkPrefix(string ip, string? expected)
    {
        Assert.Equal(expected, AdminLoginGuard.GetSubnetKey(ip));
    }

    [Theory]
    [InlineData("correct-horse", "correct-horse", true)]
    [InlineData("correct-hors", "correct-horse", false)]
    [InlineData("Correct-horse", "correct-horse", false)]
    [InlineData("", "correct-horse", false)]
    [InlineData(null, "correct-horse", false)]
    public void PasswordMatches_VariousCandidates_ComparesExactly(string? candidate, string expected, bool matches)
    {
        Assert.Equal(matches, AdminLoginGuard.PasswordMatches(candidate, expected));
    }

    private static AdminLoginGuard CreateGuard(out ManualTimeProvider time)
    {
        time = new ManualTimeProvider();
        return new AdminLoginGuard(time, NullLogger<AdminLoginGuard>.Instance);
    }
}
