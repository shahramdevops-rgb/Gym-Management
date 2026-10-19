using Gym.Domain.Common;
using Gym.Domain.Expenses;
using Gym.Domain.Notifications;
using Gym.Domain.Payables;
using Gym.Domain.Subscriptions;

namespace Gym.Domain.Tests.Notifications;

/// <summary>BUSINESS_RULES.md §10 <i>The four kinds</i>: who is due which SMS today.</summary>
public sealed class SmsAudienceTests
{
    private const int Sessions = 10;
    private const int Days = 30;
    private static readonly DateOnly Today = new(2026, 10, 6);
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 7, 0, 0, TimeSpan.Zero);
    private static readonly Guid MemberId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    // ---- Subscription running out ----

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void IsRunningOut_EndingWithinTheDays_IsTrue(int daysLeft)
    {
        SmsAudience.IsRunningOut(EndingIn(daysLeft), Today, daysBefore: 3).ShouldBeTrue();
    }

    [Fact]
    public void IsRunningOut_EndingADayAfterTheWindow_IsFalse()
    {
        SmsAudience.IsRunningOut(EndingIn(4), Today, daysBefore: 3).ShouldBeFalse();
    }

    [Fact]
    public void IsRunningOut_Frozen_IsFalse()
    {
        var subscription = EndingIn(2);
        subscription.Freeze(Today, maxFreezeDays: 30).IsSuccess.ShouldBeTrue();

        SmsAudience.IsRunningOut(subscription, Today, daysBefore: 3).ShouldBeFalse();
    }

    [Fact]
    public void IsRunningOut_Queued_IsFalse()
    {
        var queued = Subscription.CreateMembership(MemberId, Sessions, 1m, Today.AddDays(1)).Value;

        SmsAudience.IsRunningOut(queued, Today, daysBefore: 30).ShouldBeFalse();
    }

    [Fact]
    public void IsRunningOut_Ended_IsFalse()
    {
        SmsAudience.IsRunningOut(EndingIn(-1), Today, daysBefore: 3).ShouldBeFalse();
    }

    [Fact]
    public void IsRunningOut_Cancelled_IsFalse()
    {
        var subscription = EndingIn(2);
        subscription.Cancel("اشتباه ثبت شد", Today, Now).IsSuccess.ShouldBeTrue();

        SmsAudience.IsRunningOut(subscription, Today, daysBefore: 3).ShouldBeFalse();
    }

    [Fact]
    public void IsRunningOut_UsedUp_IsFalse()
    {
        SmsAudience.IsRunningOut(WithSessionsLeft(0, daysLeft: 2), Today, daysBefore: 3).ShouldBeFalse();
    }

    [Fact]
    public void IsRunningOut_SingleVisit_IsFalse()
    {
        var visit = Subscription.CreateSingleVisit(MemberId, 1m, Today).Value;

        SmsAudience.IsRunningOut(visit, Today, daysBefore: 30).ShouldBeFalse();
    }

    // ---- Few sessions left ----

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void HasFewSessionsLeft_AtOrBelowTheNumber_IsTrue(int left)
    {
        SmsAudience.HasFewSessionsLeft(WithSessionsLeft(left), Today, threshold: 2).ShouldBeTrue();
    }

    [Fact]
    public void HasFewSessionsLeft_AboveTheNumber_IsFalse()
    {
        SmsAudience.HasFewSessionsLeft(WithSessionsLeft(3), Today, threshold: 2).ShouldBeFalse();
    }

    [Fact]
    public void HasFewSessionsLeft_UsedUp_IsFalse()
    {
        SmsAudience.HasFewSessionsLeft(WithSessionsLeft(0), Today, threshold: 2).ShouldBeFalse();
    }

    [Fact]
    public void HasFewSessionsLeft_Frozen_IsFalse()
    {
        var subscription = WithSessionsLeft(1);
        subscription.Freeze(Today, maxFreezeDays: 30).IsSuccess.ShouldBeTrue();

        SmsAudience.HasFewSessionsLeft(subscription, Today, threshold: 2).ShouldBeFalse();
    }

    [Fact]
    public void HasFewSessionsLeft_SingleVisit_IsFalse()
    {
        var visit = Subscription.CreateSingleVisit(MemberId, 1m, Today).Value;

        SmsAudience.HasFewSessionsLeft(visit, Today, threshold: 10).ShouldBeFalse();
    }

    // ---- Birthday ----

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void BirthdayToGreet_WithinTheDaysAhead_IsThatBirthday(int daysAhead)
    {
        var birthday = Today.AddDays(daysAhead);
        var (_, month, day) = JalaliCalendar.Parts(birthday);

        SmsAudience.BirthdayToGreet(BornOn(month, day), Today, daysBefore: 3).ShouldBe(birthday);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void BirthdayToGreet_DayItself_IsNone(int daysBefore)
    {
        // Never on the day: the member hears of the free visit in time (task 10.8).
        SmsAudience.BirthdayToGreet(BornOn(7, 14), Today, daysBefore).ShouldBeNull();
    }

    [Fact]
    public void BirthdayToGreet_ADayAfterTheWindow_IsNone()
    {
        SmsAudience.BirthdayToGreet(BornOn(7, 18), Today, daysBefore: 3).ShouldBeNull();
    }

    [Fact]
    public void BirthdayToGreet_Yesterday_IsNone()
    {
        SmsAudience.BirthdayToGreet(BornOn(7, 13), Today, daysBefore: 7).ShouldBeNull();
    }

    [Fact]
    public void BirthdayToGreet_BornOn30EsfandInAYearWithout_Is29Esfand()
    {
        var born = JalaliCalendar.ToDate(1379, 12, 30);
        var today = JalaliCalendar.ToDate(1404, 12, 28);

        SmsAudience.BirthdayToGreet(born, today, daysBefore: 1).ShouldBe(JalaliCalendar.ToDate(1404, 12, 29));
    }

    [Fact]
    public void BirthdayToGreet_FarvardinSeenFromEsfand_IsNextYearsBirthday()
    {
        var today = JalaliCalendar.ToDate(1404, 12, 28);

        SmsAudience.BirthdayToGreet(JalaliCalendar.ToDate(1370, 1, 1), today, daysBefore: 7)
            .ShouldBe(JalaliCalendar.ToDate(1405, 1, 1));
    }

    [Fact]
    public void BirthdayToGreet_BornToday_IsNone()
    {
        SmsAudience.BirthdayToGreet(Today, Today, daysBefore: 7).ShouldBeNull();
    }

    [Fact]
    public void HoldsPlanForBirthday_ActivePlan_IsTrue()
    {
        SmsAudience.HoldsPlanForBirthday(WithSessionsLeft(3), Today).ShouldBeTrue();
    }

    [Fact]
    public void HoldsPlanForBirthday_Frozen_IsTrue()
    {
        var subscription = WithSessionsLeft(3);
        subscription.Freeze(Today, maxFreezeDays: 30).IsSuccess.ShouldBeTrue();

        SmsAudience.HoldsPlanForBirthday(subscription, Today).ShouldBeTrue();
    }

    [Fact]
    public void HoldsPlanForBirthday_BoughtForLater_IsTrue()
    {
        var queued = Subscription.CreateMembership(MemberId, Sessions, 1m, Today.AddDays(5)).Value;

        SmsAudience.HoldsPlanForBirthday(queued, Today).ShouldBeTrue();
    }

    [Fact]
    public void HoldsPlanForBirthday_UsedUp_IsFalse()
    {
        SmsAudience.HoldsPlanForBirthday(WithSessionsLeft(0), Today).ShouldBeFalse();
    }

    [Fact]
    public void HoldsPlanForBirthday_Ended_IsFalse()
    {
        SmsAudience.HoldsPlanForBirthday(EndingIn(-1), Today).ShouldBeFalse();
    }

    [Fact]
    public void HoldsPlanForBirthday_Cancelled_IsFalse()
    {
        var subscription = EndingIn(10);
        subscription.Cancel("اشتباه ثبت شد", Today, Now).IsSuccess.ShouldBeTrue();

        SmsAudience.HoldsPlanForBirthday(subscription, Today).ShouldBeFalse();
    }

    [Fact]
    public void HoldsPlanForBirthday_SingleVisit_IsFalse()
    {
        var visit = Subscription.CreateSingleVisit(MemberId, 1m, Today).Value;

        SmsAudience.HoldsPlanForBirthday(visit, Today).ShouldBeFalse();
    }

    // ---- Cheques and instalments ----

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void IsComingDue_PendingWithinTheDays_IsTrue(int daysAway)
    {
        SmsAudience.IsComingDue(Cheque(Today.AddDays(daysAway)), Today, daysBefore: 3).ShouldBeTrue();
    }

    [Fact]
    public void IsComingDue_ADayAfterTheWindow_IsFalse()
    {
        SmsAudience.IsComingDue(Cheque(Today.AddDays(4)), Today, daysBefore: 3).ShouldBeFalse();
    }

    [Fact]
    public void IsComingDue_PastItsDate_IsFalse()
    {
        SmsAudience.IsComingDue(Cheque(Today.AddDays(-1)), Today, daysBefore: 3).ShouldBeFalse();
    }

    [Fact]
    public void IsComingDue_Paid_IsFalse()
    {
        var installment = Payable.Register(
            PayableKind.Installment, 1_000_000m, Today.AddDays(2), "بانک", "وام", ExpenseCategory.CafePurchasingId, 1, 12, UserId).Value;
        installment.MarkPaid(Today, Now, UserId).IsSuccess.ShouldBeTrue();

        SmsAudience.IsComingDue(installment, Today, daysBefore: 3).ShouldBeFalse();
    }

    [Fact]
    public void IsComingDue_Cancelled_IsFalse()
    {
        var cheque = Cheque(Today.AddDays(2));
        cheque.Cancel("باطل شد", Now, UserId).IsSuccess.ShouldBeTrue();

        SmsAudience.IsComingDue(cheque, Today, daysBefore: 3).ShouldBeFalse();
    }

    // ---- The subscription receipt (task 10.9) ----

    [Theory]
    [InlineData(10, 10)]
    [InlineData(4, 4)]
    public void EarnsReceipt_PaymentClearsWhatWasOwed_IsTrue(int owedBefore, int amount)
    {
        SmsAudience.EarnsReceipt(EndingIn(20), owedBefore, amount).ShouldBeTrue();
    }

    [Fact]
    public void EarnsReceipt_PartPayment_IsFalse()
    {
        SmsAudience.EarnsReceipt(EndingIn(20), owedBefore: 10m, amount: 6m).ShouldBeFalse();
    }

    [Fact]
    public void EarnsReceipt_NothingWasOwed_IsFalse()
    {
        SmsAudience.EarnsReceipt(EndingIn(20), owedBefore: 0m, amount: 0m).ShouldBeFalse();
    }

    [Fact]
    public void EarnsReceipt_SingleVisit_IsFalse()
    {
        var visit = Subscription.CreateSingleVisit(MemberId, 150_000m, Today).Value;

        SmsAudience.EarnsReceipt(visit, owedBefore: 150_000m, amount: 150_000m).ShouldBeFalse();
    }

    [Fact]
    public void EarnsReceipt_FreePlan_IsFalse()
    {
        var free = Subscription.CreateMembership(MemberId, Sessions, 0m, Today).Value;

        SmsAudience.EarnsReceipt(free, owedBefore: 0m, amount: 0m).ShouldBeFalse();
    }

    [Fact]
    public void ReceiptStillDue_FullyPaidPlan_IsTrue()
    {
        SmsAudience.ReceiptStillDue(EndingIn(20), netPaid: Sessions).ShouldBeTrue();
    }

    [Fact]
    public void ReceiptStillDue_Cancelled_IsFalse()
    {
        var subscription = EndingIn(20);
        subscription.Cancel("اشتباه ثبت شد", Today, Now).IsSuccess.ShouldBeTrue();

        SmsAudience.ReceiptStillDue(subscription, netPaid: Sessions).ShouldBeFalse();
    }

    [Fact]
    public void ReceiptStillDue_NotFullyPaid_IsFalse()
    {
        SmsAudience.ReceiptStillDue(EndingIn(20), netPaid: Sessions - 1).ShouldBeFalse();
    }

    [Fact]
    public void IsQueuedBehindAnother_StartsTheDayAfterALivePlanEnds_IsTrue()
    {
        var current = EndingIn(5);
        var queued = Subscription.CreateMembership(MemberId, Sessions, 1m, current.EndDate.AddDays(1)).Value;

        SmsAudience.IsQueuedBehindAnother(queued, [current, queued], Today).ShouldBeTrue();
    }

    [Fact]
    public void IsQueuedBehindAnother_StartsToday_IsFalse()
    {
        var plan = EndingIn(Days - 1);

        SmsAudience.IsQueuedBehindAnother(plan, [EndingIn(-1), plan], Today).ShouldBeFalse();
    }

    [Fact]
    public void IsQueuedBehindAnother_PlanBeforeItCancelled_IsFalse()
    {
        // Its dates no longer move: nothing is before it (§4).
        var current = EndingIn(5);
        current.Cancel("اشتباه ثبت شد", Today, Now).IsSuccess.ShouldBeTrue();
        var queued = Subscription.CreateMembership(MemberId, Sessions, 1m, current.EndDate.AddDays(1)).Value;

        SmsAudience.IsQueuedBehindAnother(queued, [current], Today).ShouldBeFalse();
    }

    [Fact]
    public void IsQueuedBehindAnother_OnlyASingleVisitBeforeIt_IsFalse()
    {
        var visit = Subscription.CreateSingleVisit(MemberId, 1m, Today).Value;
        var upcoming = Subscription.CreateMembership(MemberId, Sessions, 1m, Today.AddDays(1)).Value;

        SmsAudience.IsQueuedBehindAnother(upcoming, [visit], Today).ShouldBeFalse();
    }

    // ---- Welcome (task 10.10) ----

    [Fact]
    public void IsFirstPlan_NoOtherPlan_IsTrue()
    {
        var plan = SoldAt(EndingIn(20), 0);

        SmsAudience.IsFirstPlan(plan, [plan]).ShouldBeTrue();
    }

    [Fact]
    public void IsFirstPlan_APlanSoldBefore_IsFalse()
    {
        var earlier = SoldAt(EndingIn(5), 0);
        var plan = SoldAt(Subscription.CreateMembership(MemberId, Sessions, 1m, earlier.EndDate.AddDays(1)).Value, 1);

        SmsAudience.IsFirstPlan(plan, [earlier, plan]).ShouldBeFalse();
    }

    [Fact]
    public void IsFirstPlan_APlanSoldAfter_IsStillTrue()
    {
        var plan = SoldAt(EndingIn(5), 0);
        var later = SoldAt(Subscription.CreateMembership(MemberId, Sessions, 1m, plan.EndDate.AddDays(1)).Value, 1);

        SmsAudience.IsFirstPlan(plan, [plan, later]).ShouldBeTrue();
    }

    [Fact]
    public void IsFirstPlan_TwoSoldAtTheSameMoment_ExactlyOneIsFirst()
    {
        var one = SoldAt(EndingIn(5), 0);
        var other = SoldAt(Subscription.CreateMembership(MemberId, Sessions, 1m, one.EndDate.AddDays(1)).Value, 0);

        (SmsAudience.IsFirstPlan(one, [other]) ^ SmsAudience.IsFirstPlan(other, [one])).ShouldBeTrue();
    }

    [Fact]
    public void IsFirstPlan_OnlyACancelledPlanBefore_IsTrue()
    {
        var cancelled = SoldAt(EndingIn(5), 0);
        cancelled.Cancel("اشتباه ثبت شد", Today, Now).IsSuccess.ShouldBeTrue();
        var plan = SoldAt(EndingIn(20), 1);

        SmsAudience.IsFirstPlan(plan, [cancelled, plan]).ShouldBeTrue();
    }

    [Fact]
    public void IsFirstPlan_OnlySingleVisitsBefore_IsTrue()
    {
        var visit = SoldAt(Subscription.CreateSingleVisit(MemberId, 1m, Today).Value, 0);
        var plan = SoldAt(EndingIn(20), 1);

        SmsAudience.IsFirstPlan(plan, [visit, plan]).ShouldBeTrue();
    }

    [Fact]
    public void IsFirstPlan_SingleVisit_IsFalse()
    {
        var visit = SoldAt(Subscription.CreateSingleVisit(MemberId, 1m, Today).Value, 0);

        SmsAudience.IsFirstPlan(visit, [visit]).ShouldBeFalse();
    }

    [Fact]
    public void IsFirstPlan_Cancelled_IsFalse()
    {
        var plan = SoldAt(EndingIn(20), 0);
        plan.Cancel("اشتباه ثبت شد", Today, Now).IsSuccess.ShouldBeTrue();

        SmsAudience.IsFirstPlan(plan, [plan]).ShouldBeFalse();
    }

    [Fact]
    public void IsFirstPlan_PricedAtZero_IsTrue()
    {
        var free = SoldAt(Subscription.CreateMembership(MemberId, Sessions, 0m, Today).Value, 0);

        SmsAudience.IsFirstPlan(free, [free]).ShouldBeTrue();
    }

    // ---- The debt reminder (task 10.11) ----

    [Theory]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(45, true)]
    public void IsDebtReminderDue_OldestSaleAge_IsDueOnceItIsAtLeastTheDays(int daysOld, bool expected)
    {
        SmsAudience.IsDebtReminderDue(Debt(500_000m, daysOld), lastReminderAt: null, Today, daysAfter: 30, minAmount: 100_000m)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsDebtReminderDue_OneDayAfterYesterdaysSale_IsDue()
    {
        SmsAudience.IsDebtReminderDue(Debt(5_000m, 1), lastReminderAt: null, Today, daysAfter: 1, minAmount: 1_000m)
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData(99_999, false)]
    [InlineData(100_000, true)]
    public void IsDebtReminderDue_WholeDebtAgainstTheMinimum_IsDueFromTheMinimumUp(int owed, bool expected)
    {
        SmsAudience.IsDebtReminderDue(Debt(owed, 40), lastReminderAt: null, Today, daysAfter: 30, minAmount: 100_000m)
            .ShouldBe(expected);
    }

    [Fact]
    public void IsDebtReminderDue_NothingOwed_IsNotDue()
    {
        SmsAudience.IsDebtReminderDue(Debt(0m, 40), lastReminderAt: null, Today, daysAfter: 30, minAmount: 1_000m)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsDebtReminderDue_RemindedAfterTheOldestSale_IsNotDueAgain()
    {
        var debt = Debt(500_000m, 40);

        SmsAudience.IsDebtReminderDue(debt, debt.OldestSoldAt.AddDays(30), Today, daysAfter: 30, minAmount: 100_000m)
            .ShouldBeFalse();
    }

    [Fact]
    public void IsDebtReminderDue_OldestSaleRecordedAfterTheLastReminder_IsDueAgain()
    {
        // Everything owed at the last reminder was paid; this debt began after it.
        var debt = Debt(300_000m, 35);

        SmsAudience.IsDebtReminderDue(debt, debt.OldestSoldAt.AddDays(-5), Today, daysAfter: 30, minAmount: 100_000m)
            .ShouldBeTrue();
    }

    // ---- Helpers ----

    /// <summary>A debt whose oldest unpaid sale was recorded <paramref name="daysOld"/> days before today, at noon UTC.</summary>
    private static OpenDebt Debt(decimal owed, int daysOld)
    {
        var day = Today.AddDays(-daysOld);

        return new OpenDebt(owed, new DateTimeOffset(day.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero), day);
    }

    /// <summary>
    /// Stamps the moment it was saved, <paramref name="minutes"/> after <see cref="Now"/>, as the
    /// database interceptor does; the entity has no setter of its own for it.
    /// </summary>
    private static Subscription SoldAt(Subscription subscription, int minutes)
    {
        typeof(Entity).GetProperty(nameof(Entity.CreatedAt))!.SetValue(subscription, Now.AddMinutes(minutes));

        return subscription;
    }

    /// <summary>A plan of 10 sessions, none used, whose last day is <paramref name="daysLeft"/> days from today.</summary>
    private static Subscription EndingIn(int daysLeft) =>
        Subscription.CreateMembership(MemberId, Sessions, 1m, Today.AddDays(daysLeft - Days + 1)).Value;

    /// <summary>A plan started today with <paramref name="left"/> of its 10 sessions left.</summary>
    private static Subscription WithSessionsLeft(int left, int daysLeft = 20)
    {
        var subscription = EndingIn(daysLeft);
        for (var i = 0; i < Sessions - left; i++)
        {
            subscription.ConsumeSession(Today).IsSuccess.ShouldBeTrue();
        }

        return subscription;
    }

    private static DateOnly BornOn(int jalaliMonth, int jalaliDay) => JalaliCalendar.ToDate(1370, jalaliMonth, jalaliDay);

    private static Payable Cheque(DateOnly dueDate) =>
        Payable.Register(
            PayableKind.Cheque, 12_500_000m, dueDate, "فروشگاه", "تردمیل", ExpenseCategory.CafePurchasingId, null, null, UserId).Value;
}
