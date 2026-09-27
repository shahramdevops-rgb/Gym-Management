using Gym.Domain.Pricing;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Infrastructure;

/// <summary>
/// A plan the desk would build (BUSINESS_RULES.md §3): so many days, so many sessions. Posting
/// <see cref="Body"/> to <c>/api/members/{id}/subscriptions</c> sells it at the session price of the
/// moment.
/// </summary>
/// <param name="Price">What it costs at the session price <see cref="TestPlans.AddAsync"/> set.</param>
internal sealed record TestPlan(int DurationDays, int SessionCount, decimal Price)
{
    /// <summary>The body of an assign request.</summary>
    public object Body => new { durationDays = DurationDays, sessionCount = SessionCount };
}

/// <summary>
/// Plans and prices set up as a precondition for testing something else — a check-in, a payment —
/// rather than to test pricing itself. Since task 6.5.6 there is no plan row to insert: a test sets
/// the gym's session price and describes the plan it will sell.
/// </summary>
internal static class TestPlans
{
    /// <summary>
    /// Sets the session price so that <paramref name="sessions"/> sessions cost exactly
    /// <paramref name="price"/>, and returns that plan. The defaults are the plan most tests sold
    /// before 6.5.6 — 30 days, 12 sessions, 900,000 — so their expected amounts still hold.
    /// </summary>
    /// <remarks>
    /// The price is the gym's one session price, so a test that needs two plans at different rates
    /// must sell the first before setting the second.
    /// </remarks>
    internal static async Task<TestPlan> AddAsync(
        DatabaseFixture fixture, int durationDays = 30, int sessions = 12, decimal price = 900_000m)
    {
        var sessionPrice = price / sessions;
        if (decimal.Round(sessionPrice, PriceList.PriceDecimals) != sessionPrice)
        {
            throw new ArgumentException($"{price} is not a whole number of cents per session for {sessions} sessions.", nameof(price));
        }

        await SetPricesAsync(fixture, sessionPrice: sessionPrice);

        return new TestPlan(durationDays, sessions, price);
    }

    /// <summary>
    /// Writes the prices straight to the one row, the way the Owner's screen would leave them. A
    /// price left out keeps its current value.
    /// </summary>
    internal static async Task SetPricesAsync(
        DatabaseFixture fixture, decimal? sessionPrice = null, decimal? singleVisitPrice = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await using var scope = fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"""
            UPDATE price_lists
            SET session_price = COALESCE({sessionPrice}, session_price),
                single_visit_price = COALESCE({singleVisitPrice}, single_visit_price)
            """,
            TestContext.Current.CancellationToken);
    }
}
