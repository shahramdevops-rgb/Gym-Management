using Gym.Domain.Attendances;
using Gym.Domain.Common;
using Gym.Domain.Subscriptions;

namespace Gym.Domain.Tests.Attendances;

/// <summary>BUSINESS_RULES.md §7 <i>Birthday visit</i>: which visit on the birthday is free.</summary>
public sealed class BirthdayVisitTests
{
    private static readonly Guid MemberId = Guid.NewGuid();

    // 1405/07/14.
    private static readonly DateOnly Today = new(2026, 10, 6);

    private static readonly DateOnly BornToday = JalaliCalendar.ToDate(1370, 7, 14);

    [Fact]
    public void FreeYear_PlanOnTheBirthday_IsThisJalaliYear()
    {
        BirthdayVisit.FreeYear(BornToday, Plan(), Today).ShouldBe(1405);
    }

    [Fact]
    public void FreeYear_PlanOnAnotherDay_IsNull()
    {
        BirthdayVisit.FreeYear(JalaliCalendar.ToDate(1370, 7, 15), Plan(), Today).ShouldBeNull();
    }

    [Fact]
    public void FreeYear_SingleVisitOnTheBirthday_IsNull()
    {
        // Its money is already paid: it is used as usual.
        var visit = Subscription.CreateSingleVisit(MemberId, 1m, Today).Value;

        BirthdayVisit.FreeYear(BornToday, visit, Today).ShouldBeNull();
    }

    [Fact]
    public void FreeYear_FrozenPlanOnTheBirthday_IsThisJalaliYear()
    {
        // Check-in unfreezes it first; the visit is still the free one.
        var plan = Plan();
        plan.Freeze(Today, maxFreezeDays: 30).IsSuccess.ShouldBeTrue();

        BirthdayVisit.FreeYear(BornToday, plan, Today).ShouldBe(1405);
    }

    [Fact]
    public void FreeYear_TheDayOfBirth_IsNull()
    {
        BirthdayVisit.FreeYear(Today, Plan(), Today).ShouldBeNull();
    }

    private static Subscription Plan() =>
        Subscription.CreateMembership(MemberId, 10, 1m, Today.AddDays(-3)).Value;
}
