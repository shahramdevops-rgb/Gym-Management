using Gym.Domain.Attendances;

namespace Gym.Domain.Tests.Attendances;

/// <summary>BUSINESS_RULES.md §12 <i>Needs attention</i> and §10 <i>The absence SMS</i>: how long a member has been away.</summary>
public sealed class AbsenceTests
{
    private static readonly DateOnly Today = new(2026, 10, 19);
    private static readonly DateOnly PlanStart = new(2026, 10, 1);

    [Fact]
    public void Since_VisitAfterThePlanStarted_IsTheVisit() =>
        Absence.Since(new DateOnly(2026, 10, 5), PlanStart).ShouldBe(new DateOnly(2026, 10, 5));

    [Fact]
    public void Since_NoVisitAtAll_IsThePlanStart() =>
        Absence.Since(null, PlanStart).ShouldBe(PlanStart);

    [Fact]
    public void Since_LastVisitBeforeThePlanStarted_IsThePlanStart() =>
        Absence.Since(new DateOnly(2026, 9, 10), PlanStart).ShouldBe(PlanStart);

    [Fact]
    public void Since_VisitOnTheStartDay_IsThatDay() =>
        Absence.Since(PlanStart, PlanStart).ShouldBe(PlanStart);

    [Fact]
    public void DaysAway_CameToday_IsZero() =>
        Absence.DaysAway(Today, PlanStart, Today).ShouldBe(0);

    [Fact]
    public void DaysAway_NeverCame_CountsFromThePlanStart() =>
        Absence.DaysAway(null, PlanStart, Today).ShouldBe(18);
}
