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

    [Fact]
    public void BirthdayToGreet_DayItselfWithZero_IsToday()
    {
        SmsAudience.BirthdayToGreet(BornOn(7, 14), Today, daysBefore: 0).ShouldBe(Today);
    }

    [Fact]
    public void BirthdayToGreet_TomorrowWithZero_IsNone()
    {
        SmsAudience.BirthdayToGreet(BornOn(7, 15), Today, daysBefore: 0).ShouldBeNull();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void BirthdayToGreet_WithinTheDaysAhead_IsThatBirthday(int daysAhead)
    {
        var birthday = Today.AddDays(daysAhead);
        var (_, month, day) = JalaliCalendar.Parts(birthday);

        SmsAudience.BirthdayToGreet(BornOn(month, day), Today, daysBefore: 3).ShouldBe(birthday);
    }

    [Fact]
    public void BirthdayToGreet_DayItselfWithDaysAhead_IsNone()
    {
        // The «پیشاپیش» greeting never lands on the day (decided with the developer, task 10.3).
        SmsAudience.BirthdayToGreet(BornOn(7, 14), Today, daysBefore: 3).ShouldBeNull();
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
        var today = JalaliCalendar.ToDate(1404, 12, 29);

        SmsAudience.BirthdayToGreet(born, today, daysBefore: 0).ShouldBe(today);
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
        SmsAudience.BirthdayToGreet(Today, Today, daysBefore: 0).ShouldBeNull();
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

    // ---- Helpers ----

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
