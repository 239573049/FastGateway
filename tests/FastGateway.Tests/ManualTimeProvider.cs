namespace FastGateway.Tests;

internal sealed class ManualTimeProvider(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public ManualTimeProvider() : this(new DateTimeOffset(2026, 10, 24, 0, 0, 0, TimeSpan.Zero))
    {
    }

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan delta) => _now += delta;
}
