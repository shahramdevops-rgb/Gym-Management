using Gym.Domain.Plans;

namespace Gym.Domain.Tests.Plans;

/// <summary>BUSINESS_RULES.md §3: the single-session plan's shape is not a choice.</summary>
public sealed class SingleSessionPlanTests
{
    [Fact]
    public void Create_SingleSessionOneDayOneSession_Succeeds()
    {
        var plan = Plan.Create("تک‌جلسه‌ای", 1, 1, 150_000m, PlanKind.SingleSession).Value;

        plan.Kind.ShouldBe(PlanKind.SingleSession);
        plan.IsSingleSession.ShouldBeTrue();
        plan.DurationDays.ShouldBe(1);
        plan.SessionCount.ShouldBe(1);
    }

    [Fact]
    public void Create_WithoutAKind_IsAMembership()
    {
        var plan = Plan.Create("یک ماهه", 30, 12, 900_000m).Value;

        plan.Kind.ShouldBe(PlanKind.Membership);
        plan.IsSingleSession.ShouldBeFalse();
    }

    [Theory]
    [InlineData(2, 1)]
    [InlineData(30, 1)]
    [InlineData(1, 2)]
    [InlineData(1, 12)]
    public void Create_SingleSessionWithTheWrongShape_FailsWithSingleSessionShape(int durationDays, int sessionCount)
    {
        Plan.Create("تک‌جلسه‌ای", durationDays, sessionCount, 150_000m, PlanKind.SingleSession)
            .Error.ShouldBe(PlanErrors.SingleSessionShape);
    }

    [Fact]
    public void Create_SingleSessionUnlimited_FailsWithSingleSessionShape()
    {
        Plan.Create("تک‌جلسه‌ای", 1, null, 150_000m, PlanKind.SingleSession)
            .Error.ShouldBe(PlanErrors.SingleSessionShape);
    }

    [Fact]
    public void Update_SingleSessionPlanNameAndPrice_Succeeds()
    {
        var plan = Plan.Create("تک‌جلسه‌ای", 1, 1, 150_000m, PlanKind.SingleSession).Value;

        // The only supported way to change the walk-in rate (BUSINESS_RULES.md §3).
        plan.Update("ورود آزاد", 1, 1, 200_000m).IsSuccess.ShouldBeTrue();

        plan.Price.ShouldBe(200_000m);
        plan.Kind.ShouldBe(PlanKind.SingleSession);
    }

    [Fact]
    public void Update_SingleSessionPlanToMoreDays_FailsWithSingleSessionShape()
    {
        var plan = Plan.Create("تک‌جلسه‌ای", 1, 1, 150_000m, PlanKind.SingleSession).Value;

        plan.Update("تک‌جلسه‌ای", 30, 1, 150_000m).Error.ShouldBe(PlanErrors.SingleSessionShape);
    }

    [Fact]
    public void Update_SingleSessionPlanToMoreSessions_FailsWithSingleSessionShape()
    {
        var plan = Plan.Create("تک‌جلسه‌ای", 1, 1, 150_000m, PlanKind.SingleSession).Value;

        plan.Update("تک‌جلسه‌ای", 1, 10, 150_000m).Error.ShouldBe(PlanErrors.SingleSessionShape);
    }

    [Fact]
    public void Update_MembershipToOneDayOneSession_IsAllowed()
    {
        // A membership is free to be short. Only the single-session kind has a fixed shape.
        var plan = Plan.Create("یک ماهه", 30, 12, 900_000m).Value;

        plan.Update("یک روزه", 1, 1, 100_000m).IsSuccess.ShouldBeTrue();
        plan.Kind.ShouldBe(PlanKind.Membership);
    }
}
