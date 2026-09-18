using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Subscriptions;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Subscriptions;

/// <summary>
/// The <c>subscriptions</c> table enforces the rules by itself (CLAUDE.md: invariants are enforced
/// by the database too). Rows are written with raw SQL, past every C# check, to prove it.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SubscriptionTableTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static readonly DateOnly Start = new(2026, 9, 1);

    [Fact]
    public async Task Insert_OverlappingDatesForOneMember_RejectedByTheExclusionConstraint()
    {
        var (memberId, planId) = await AddMemberAndPlanAsync();
        await InsertAsync(memberId, planId, Start, Start.AddDays(29));

        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertAsync(memberId, planId, Start.AddDays(29), Start.AddDays(58)));

        exception.SqlState.ShouldBe(PostgresErrorCodes.ExclusionViolation);
        exception.ConstraintName.ShouldBe(SubscriptionConstraints.NoOverlap);
    }

    [Fact]
    public async Task Insert_AdjacentDates_Allowed()
    {
        var (memberId, planId) = await AddMemberAndPlanAsync();
        await InsertAsync(memberId, planId, Start, Start.AddDays(29));

        await InsertAsync(memberId, planId, Start.AddDays(30), Start.AddDays(59));
    }

    [Fact]
    public async Task Insert_OverlapWithACancelledSubscription_Allowed()
    {
        var (memberId, planId) = await AddMemberAndPlanAsync();
        await InsertAsync(memberId, planId, Start, Start.AddDays(29), cancelled: true);

        await InsertAsync(memberId, planId, Start, Start.AddDays(29));
    }

    [Fact]
    public async Task Insert_SameDatesForDifferentMembers_Allowed()
    {
        var (firstMember, planId) = await AddMemberAndPlanAsync();
        var (secondMember, _) = await AddMemberAndPlanAsync(planName: "دیگر", phone: "+989351234567");
        await InsertAsync(firstMember, planId, Start, Start.AddDays(29));

        await InsertAsync(secondMember, planId, Start, Start.AddDays(29));
    }

    [Theory]
    [InlineData(12, 13, "ck_subscriptions_used_sessions")]
    [InlineData(12, -1, "ck_subscriptions_used_sessions")]
    public async Task Insert_ImpossibleSessionCount_RejectedByACheckConstraint(int total, int used, string constraint)
    {
        var (memberId, planId) = await AddMemberAndPlanAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertAsync(memberId, planId, Start, Start.AddDays(29), totalSessions: total, usedSessions: used));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe(constraint);
    }

    [Fact]
    public async Task Insert_UnlimitedWithManySessionsUsed_Allowed()
    {
        var (memberId, planId) = await AddMemberAndPlanAsync();

        await InsertAsync(memberId, planId, Start, Start.AddDays(29), totalSessions: null, usedSessions: 400);
    }

    [Fact]
    public async Task Insert_EndBeforeStart_RejectedByACheckConstraint()
    {
        var (memberId, planId) = await AddMemberAndPlanAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertAsync(memberId, planId, Start, Start.AddDays(-1)));

        exception.ConstraintName.ShouldBe("ck_subscriptions_dates");
    }

    [Fact]
    public async Task Update_CancelledWithoutAReason_RejectedByACheckConstraint()
    {
        var (memberId, planId) = await AddMemberAndPlanAsync();
        var id = await InsertAsync(memberId, planId, Start, Start.AddDays(29));

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteAsync($"UPDATE subscriptions SET cancelled_at = now() WHERE id = '{id}'"));

        exception.ConstraintName.ShouldBe("ck_subscriptions_cancellation");
    }

    [Fact]
    public async Task Delete_MemberWithASubscription_RejectedByTheForeignKey()
    {
        var (memberId, planId) = await AddMemberAndPlanAsync();
        await InsertAsync(memberId, planId, Start, Start.AddDays(29));

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteAsync($"DELETE FROM members WHERE id = '{memberId}'"));

        // ON DELETE RESTRICT reports 23001 restrict_violation, not 23503.
        exception.SqlState.ShouldBe(PostgresErrorCodes.RestrictViolation);
    }

    [Fact]
    public async Task Save_StaleCopyAfterAnotherSave_ThrowsConcurrencyException()
    {
        var (memberId, planId) = await AddMemberAndPlanAsync();
        var id = await InsertAsync(memberId, planId, Start, Start.AddDays(29));
        var today = Start.AddDays(5);

        await using var firstScope = Fixture.CreateScope();
        await using var secondScope = Fixture.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstCopy = await first.Subscriptions.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);
        var secondCopy = await second.Subscriptions.SingleAsync(s => s.Id == id, TestContext.Current.CancellationToken);

        // Two check-ins read the same row; the first to save wins.
        firstCopy.ConsumeSession(today).IsSuccess.ShouldBeTrue();
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        secondCopy.ConsumeSession(today).IsSuccess.ShouldBeTrue();

        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => second.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private async Task<(Guid MemberId, Guid PlanId)> AddMemberAndPlanAsync(
        string planName = "ماهانه", string phone = "+989121234567")
    {
        var member = Member.Create("رضا احمدی", phone, null).Value;
        var plan = Plan.Create(planName, 30, 12, 900_000m).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        db.Plans.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (member.Id, plan.Id);
    }

    private async Task<Guid> InsertAsync(
        Guid memberId, Guid planId, DateOnly start, DateOnly end, int? totalSessions = 12, int usedSessions = 0, bool cancelled = false)
    {
        var id = Guid.CreateVersion7();
        var cancelledAt = cancelled ? "now()" : "NULL";
        var reason = cancelled ? "'لغو'" : "NULL";
        var total = totalSessions?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "NULL";

        await ExecuteAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, plan_id, plan_name, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, total_frozen_days,
                                       cancelled_at, cancellation_reason, created_at)
            VALUES ('{id}', '{memberId}', '{planId}', 'پلن', 900000, 30, {total},
                    '{start:yyyy-MM-dd}', '{end:yyyy-MM-dd}', {usedSessions}, 0,
                    {cancelledAt}, {reason}, now())
            """);

        return id;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
