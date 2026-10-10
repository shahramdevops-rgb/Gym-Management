namespace Gym.Api.Admin.Demo;

/// <summary>
/// The clock <c>admin seed-demo</c> runs on: it starts at the real moment and the seeder moves it.
/// </summary>
/// <remarks>
/// Every handler stamps what it writes with the injected <see cref="TimeProvider"/>: a payment's
/// <c>PaidAt</c>, a visit's check-in, an audit row. There is no way to send a date in (BUSINESS_RULES.md
/// §5: "there is no way to record a backdated payment"), so data written through the real handlers
/// all lands on today. The seeder sets this clock back ninety days and walks it forward instead,
/// so three months of visits and sales go through the same rules the desk does, each at its own
/// moment. It only ever moves forward and never past <see cref="RealStart"/>.
/// </remarks>
public sealed class DemoClock : TimeProvider
{
    private DateTimeOffset _now;

    public DemoClock(TimeProvider real)
    {
        ArgumentNullException.ThrowIfNull(real);

        RealStart = real.GetUtcNow();
        _now = RealStart;
    }

    /// <summary>The real moment the command started. The demo's last event happens before it.</summary>
    public DateTimeOffset RealStart { get; }

    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>Moves the clock to <paramref name="moment"/>.</summary>
    public void Set(DateTimeOffset moment)
    {
        if (moment > RealStart)
        {
            throw new InvalidOperationException($"The demo clock cannot pass the real time ({RealStart:O}).");
        }

        _now = moment.ToUniversalTime();
    }
}
