using Gym.Domain.Subscriptions;

namespace Gym.Domain.Tests.Subscriptions;

/// <summary>
/// BUSINESS_RULES.md §4 <i>Single-session subscriptions</i>: a single visit is an ordinary
/// subscription that every calendar rule ignores. These tests are mostly about what must
/// <b>not</b> happen — the damage a single visit could do to a membership sold earlier.
/// </summary>
/// <remarks>
/// Where a single visit starts is not tested against a calendar any more: since task 6.5.6
/// <see cref="Subscription.CreateSingleVisit"/> takes today and nothing else, so it cannot read, queue
/// behind or close anything. The integration tests check the same through the endpoint.
/// </remarks>
public sealed class SingleSessionSubscriptionTests
{
    private const int MaxFreezeDays = 30;

    private static readonly Guid MemberId = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 9, 10);

    [Fact]
    public void CreateSingleVisit_Always_IsFlaggedAndCoversOnlyToday()
    {
        var visit = SellSingleVisit();

        visit.IsSingleSession.ShouldBeTrue();
        visit.StartDate.ShouldBe(Today);
        visit.EndDate.ShouldBe(Today);
        visit.TotalSessions.ShouldBe(1);
    }

    [Fact]
    public void NextStartDate_MembershipSoldAfterASingleVisitToday_StartsToday()
    {
        // The visitor buys a plan an hour after dropping in. The single visit covers today, but it
        // must not push the membership to tomorrow.
        var visit = SellSingleVisit();
        visit.ConsumeSession(Today);

        SubscriptionSchedule.NextStartDate(Today, [visit]).ShouldBe(Today);
    }

    [Fact]
    public void NextStartDate_UsedSingleVisitAndAnExhaustedMembership_ClosesOnlyTheMembership()
    {
        // The pack ran out of sessions on the 5th; the visit on the 10th is used too. Selling a new
        // plan closes the pack early and never mistakes the used visit for "the exhausted one".
        var exhausted = SellExhausted(new DateOnly(2026, 9, 1), usedOn: new DateOnly(2026, 9, 5));
        var visit = SellSingleVisit();
        visit.ConsumeSession(Today);

        SubscriptionSchedule.NextStartDate(Today, [exhausted, visit]).ShouldBe(Today);

        exhausted.EndDate.ShouldBe(Today.AddDays(-1));
        visit.EndDate.ShouldBe(Today);
    }

    [Fact]
    public void InEffectToday_SingleVisitOnly_IsThatVisit()
    {
        var visit = SellSingleVisit();

        SubscriptionSchedule.InEffectToday(Today, [visit]).ShouldBe(visit);
    }

    [Fact]
    public void InEffectToday_UsedSingleVisitOnly_IsNull()
    {
        var visit = SellSingleVisit();
        visit.ConsumeSession(Today);

        SubscriptionSchedule.InEffectToday(Today, [visit]).ShouldBeNull();
    }

    [Fact]
    public void InEffectToday_ActiveSingleVisitAndActiveMembership_UsesTheSingleVisit()
    {
        // The visit is worth nothing tomorrow; the membership's sessions keep.
        var membership = SellMembership(new DateOnly(2026, 9, 1));
        var visit = SellSingleVisit();

        SubscriptionSchedule.InEffectToday(Today, [membership, visit]).ShouldBe(visit);
    }

    [Fact]
    public void InEffectToday_UsedSingleVisitAndQueuedMembership_DoesNotMoveTheQueue()
    {
        // A used single visit is exhausted by design. Letting it trigger the promotion would drag
        // a membership queued for next month into starting today.
        var visit = SellSingleVisit();
        visit.ConsumeSession(Today);
        var queued = SellMembership(new DateOnly(2026, 10, 1));

        SubscriptionSchedule.InEffectToday(Today, [visit, queued]).ShouldBeNull();

        queued.StartDate.ShouldBe(new DateOnly(2026, 10, 1));
        queued.EndDate.ShouldBe(new DateOnly(2026, 10, 30));
    }

    [Fact]
    public void InEffectToday_SingleVisitAlongsideAFrozenMembership_IsTheSingleVisit()
    {
        // What the front desk actually does: the member is away, their pack is frozen, they come in
        // once and buy a visit. The freeze is untouched (BUSINESS_RULES.md §4).
        var membership = SellMembership(new DateOnly(2026, 9, 1));
        membership.Freeze(Today, MaxFreezeDays);
        var visit = SellSingleVisit();

        SubscriptionSchedule.InEffectToday(Today, [membership, visit]).ShouldBe(visit);

        membership.FrozenSince.ShouldBe(Today);
        membership.EndDate.ShouldBe(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void Freeze_SingleVisit_FailsWithSingleSessionNotFreezable()
    {
        var visit = SellSingleVisit();

        visit.Freeze(Today, MaxFreezeDays).Error.ShouldBe(SubscriptionErrors.SingleSessionNotFreezable);
        visit.FrozenSince.ShouldBeNull();
    }

    [Fact]
    public void ConsumeSession_SingleVisitTwice_FailsTheSecondTime()
    {
        var visit = SellSingleVisit();

        visit.ConsumeSession(Today).IsSuccess.ShouldBeTrue();
        visit.ConsumeSession(Today).Error.ShouldBe(SubscriptionErrors.NoSessionsLeft);
    }

    private static Subscription SellSingleVisit() =>
        Subscription.CreateSingleVisit(MemberId, 150_000m, Today).Value;

    /// <summary>A 30-day, 12-session plan.</summary>
    private static Subscription SellMembership(DateOnly start) =>
        Subscription.CreateMembership(MemberId, 30, 12, 100_000m, start).Value;

    /// <summary>The smallest plan (5 sessions, 30 days), every session used on <paramref name="usedOn"/>.</summary>
    private static Subscription SellExhausted(DateOnly start, DateOnly usedOn)
    {
        var subscription = Subscription.CreateMembership(MemberId, 30, Subscription.MinSessionCount, 100_000m, start).Value;
        for (var visit = 0; visit < Subscription.MinSessionCount; visit++)
        {
            subscription.ConsumeSession(usedOn).IsSuccess.ShouldBeTrue();
        }

        return subscription;
    }
}
