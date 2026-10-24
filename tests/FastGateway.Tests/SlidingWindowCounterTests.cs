using FastGateway.Risk;
using Xunit;

namespace FastGateway.Tests;

public sealed class SlidingWindowCounterTests
{
    private static readonly long Start = new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero).UtcTicks;

    [Fact]
    public void Add_WithinWindow_Accumulates()
    {
        var counter = new SlidingWindowCounter(TimeSpan.FromMinutes(10), 10);

        counter.Add(Start);
        counter.Add(Start + TimeSpan.FromMinutes(3).Ticks);
        var total = counter.Add(Start + TimeSpan.FromMinutes(8).Ticks);

        Assert.Equal(3, total);
    }

    [Fact]
    public void Count_AfterWindowElapses_DropsOldBuckets()
    {
        var counter = new SlidingWindowCounter(TimeSpan.FromMinutes(10), 10);
        counter.Add(Start);
        counter.Add(Start + TimeSpan.FromMinutes(5).Ticks);

        Assert.Equal(1, counter.Count(Start + TimeSpan.FromMinutes(10).Ticks));
        Assert.Equal(0, counter.Count(Start + TimeSpan.FromMinutes(15).Ticks));
    }

    [Fact]
    public void Add_ReusingSlotAfterFullCycle_ResetsStaleCount()
    {
        var counter = new SlidingWindowCounter(TimeSpan.FromMinutes(10), 10);
        for (var i = 0; i < 5; i++) counter.Add(Start);

        Assert.Equal(1, counter.Add(Start + TimeSpan.FromMinutes(10).Ticks));
    }

    [Fact]
    public void DistinctWindowSet_SameValueTwice_CountsOnce()
    {
        var set = new DistinctWindowSet(TimeSpan.FromMinutes(10), 64);

        set.Add("a", Start);
        set.Add("a", Start + 1);

        Assert.Equal(2, set.Add("b", Start + 2));
    }

    [Fact]
    public void DistinctWindowSet_BeyondCapacity_SaturatesAtCapacity()
    {
        var set = new DistinctWindowSet(TimeSpan.FromMinutes(10), 3);

        for (var i = 0; i < 10; i++) set.Add($"value-{i}", Start);

        Assert.Equal(3, set.Count(Start));
    }

    [Fact]
    public void DistinctWindowSet_AfterWindow_ExpiresValues()
    {
        var set = new DistinctWindowSet(TimeSpan.FromMinutes(10), 64);
        set.Add("a", Start);
        set.Add("b", Start + TimeSpan.FromMinutes(5).Ticks);

        Assert.Equal(1, set.Count(Start + TimeSpan.FromMinutes(10).Ticks));
    }
}
