using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;

namespace Gym.Domain.Tests.Subscriptions;

/// <summary>
/// BUSINESS_RULES.md §4 <i>Single-session subscriptions</i>: a single visit is an ordinary
/// subscription that every calendar rule ignores. These tests are mostly about what must
/// <b>not</b> happen — the damage a single visit could do to a membership sold earlier.
/// </summary>
public sealed class SingleSessionSubscriptionTests
{
    private const int MaxFreezeDays = 30;

    private static readonly Guid MemberId = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 9, 10);

    [Fact]
    public void Create_FromASingleSessionPlan_IsFlaggedAndCoversOnlyToday()
    {
        var visit = SellSingleVisit();

        visit.IsSingleSession.ShouldBeTrue();
        visit.StartDate.ShouldBe(Today);
        visit.EndDate.ShouldBe(Today);
        visit.TotalSessions.ShouldBe(1);
    }

    [Fact]
    public void StartDateFor_SingleVisitWithACurrentMembership_IsToday()
    {
        // The membership runs to 2026-09-30, so the queue rule would say 2026-10-01.
        var membership = SellMembership(new DateOnly(2026, 9, 1));

        SubscriptionSchedule.StartDateFor(PlanKind.SingleSession, Today, [membership]).ShouldBe(Today);
    }

    [Fact]
    public void StartDateFor_SingleVisitWithAQueuedMembership_IsToday()
    {
        var membership = SellMembership(new DateOnly(2026, 9, 1));
        var queued = SellMembership(new DateOnly(2026, 10, 1));

        SubscriptionSchedule.StartDateFor(PlanKind.SingleSession, Today, [membership, queued]).ShouldBe(Today);
    }

    [Fact]
    public void StartDateFor_SecondSingleVisitOnTheSameDay_IsAlsoToday()
    {
        var firstVisit = SellSingleVisit();
        firstVisit.ConsumeSession(Today);

        SubscriptionSchedule.StartDateFor(PlanKind.SingleSession, Today, [firstVisit]).ShouldBe(Today);
    }

    [Fact]
    public void StartDateFor_SingleVisitWithAnExhaustedMembership_LeavesThatMembershipAlone()
    {
        // The case that cost the member money: their pack ran out of sessions on the 5th but the
        // term runs to the 30th, so the ordinary rule would close it "yesterday" to free up today.
        var exhausted = SellMembership(new DateOnly(2026, 9, 1), sessions: 1);
        exhausted.ConsumeSession(new DateOnly(2026, 9, 5));

        SubscriptionSchedule.StartDateFor(PlanKind.SingleSession, Today, [exhausted]).ShouldBe(Today);

        exhausted.EndDate.ShouldBe(new DateOnly(2026, 9, 30));
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

    private static Subscription SellSingleVisit()
    {
        var plan = Plan.Create("تک‌جلسه‌ای", 1, 1, 150_000m, PlanKind.SingleSession).Value;

        return Subscription.Create(MemberId, plan, Today).Value;
    }

    private static Subscription SellMembership(DateOnly start, int? sessions = 12)
    {
        var plan = Plan.Create("پلن", 30, sessions, 900_000m).Value;

        return Subscription.Create(MemberId, plan, start).Value;
    }
}
