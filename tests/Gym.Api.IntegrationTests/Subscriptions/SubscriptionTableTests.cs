using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Subscriptions;
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
        var memberId = await AddMemberAsync();
        await InsertAsync(memberId, Start, Start.AddDays(29));

        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertAsync(memberId, Start.AddDays(29), Start.AddDays(58)));

        exception.SqlState.ShouldBe(PostgresErrorCodes.ExclusionViolation);
        exception.ConstraintName.ShouldBe(SubscriptionConstraints.NoOverlap);
    }

    [Fact]
    public async Task Insert_AdjacentDates_Allowed()
    {
        var memberId = await AddMemberAsync();
        await InsertAsync(memberId, Start, Start.AddDays(29));

        await InsertAsync(memberId, Start.AddDays(30), Start.AddDays(59));
    }

    [Fact]
    public async Task Insert_OverlapWithACancelledSubscription_Allowed()
    {
        var memberId = await AddMemberAsync();
        await InsertAsync(memberId, Start, Start.AddDays(29), cancelled: true);

        await InsertAsync(memberId, Start, Start.AddDays(29));
    }

    [Fact]
    public async Task Insert_SingleSessionOverlappingAMembership_Allowed()
    {
        // BUSINESS_RULES.md §4: a single visit is outside the no-overlap rule. Proved against the
        // constraint itself, because this is the one invariant the feature deliberately relaxes.
        var memberId = await AddMemberAsync();
        await InsertAsync(memberId, Start, Start.AddDays(29));

        await InsertSingleVisitAsync(memberId, Start.AddDays(5));
    }

    [Fact]
    public async Task Insert_TwoSingleVisitsOnTheSameDay_Allowed()
    {
        // How a member comes twice in one day (BUSINESS_RULES.md §4).
        var memberId = await AddMemberAsync();
        await InsertSingleVisitAsync(memberId, Start);

        await InsertSingleVisitAsync(memberId, Start);
    }

    [Fact]
    public async Task Insert_SingleVisitWithMembershipNumbers_RejectedByACheckConstraint()
    {
        // The flag is what the scheduling rules read, so a row carrying it while describing a
        // 30-day, 10-session product would quietly opt a real membership out of the calendar.
        var memberId = await AddMemberAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertAsync(memberId, Start, Start.AddDays(29), singleSession: true));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("ck_subscriptions_single_session_shape");
    }

    [Fact]
    public async Task Insert_SameDatesForDifferentMembers_Allowed()
    {
        var firstMember = await AddMemberAsync();
        var secondMember = await AddMemberAsync(phone: "+989351234567");
        await InsertAsync(firstMember, Start, Start.AddDays(29));

        await InsertAsync(secondMember, Start, Start.AddDays(29));
    }

    [Theory]
    [InlineData(10, 11, "ck_subscriptions_used_sessions")]
    [InlineData(10, -1, "ck_subscriptions_used_sessions")]
    [InlineData(4, 0, "ck_subscriptions_total_sessions_range")] // BUSINESS_RULES.md §3: at least 5
    [InlineData(1, 0, "ck_subscriptions_total_sessions_range")] // one session is only a single visit
    public async Task Insert_ImpossibleSessionCount_RejectedByACheckConstraint(int total, int used, string constraint)
    {
        var memberId = await AddMemberAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertAsync(memberId, Start, Start.AddDays(29), totalSessions: total, usedSessions: used));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe(constraint);
    }

    [Fact]
    public async Task Insert_NoSessionCount_RejectedBecauseThereIsNoUnlimitedPlan()
    {
        var memberId = await AddMemberAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertAsync(memberId, Start, Start.AddDays(29), totalSessions: null));

        exception.SqlState.ShouldBe(PostgresErrorCodes.NotNullViolation);
    }

    [Fact]
    public async Task Insert_140SessionsFor70Days_Allowed()
    {
        // The largest plan (BUSINESS_RULES.md §3).
        var memberId = await AddMemberAsync();

        await InsertAsync(memberId, Start, Start.AddDays(69), totalSessions: 140, usedSessions: 40, durationDays: 70);
    }

    [Fact]
    public async Task Insert_141Sessions_RejectedByACheckConstraint()
    {
        var memberId = await AddMemberAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertAsync(memberId, Start, Start.AddDays(69), totalSessions: 141, durationDays: 70));

        exception.ConstraintName.ShouldBe("ck_subscriptions_total_sessions_range");
    }

    [Theory]
    [InlineData(10, 45)]
    [InlineData(11, 30)]
    [InlineData(20, 70)]
    [InlineData(21, 45)]
    public async Task Insert_DaysThatDoNotMatchTheSessions_RejectedByACheckConstraint(int sessions, int days)
    {
        // BUSINESS_RULES.md §3 (task 6.5.18): the days follow from the sessions.
        var memberId = await AddMemberAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertAsync(memberId, Start, Start.AddDays(days - 1), totalSessions: sessions, durationDays: days));

        exception.ConstraintName.ShouldBe("ck_subscriptions_duration_days_for_sessions");
    }

    [Fact]
    public async Task Insert_EndBeforeStart_RejectedByACheckConstraint()
    {
        var memberId = await AddMemberAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => InsertAsync(memberId, Start, Start.AddDays(-1)));

        exception.ConstraintName.ShouldBe("ck_subscriptions_dates");
    }

    [Fact]
    public async Task Update_CancelledWithoutAReason_RejectedByACheckConstraint()
    {
        var memberId = await AddMemberAsync();
        var id = await InsertAsync(memberId, Start, Start.AddDays(29));

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteAsync($"UPDATE subscriptions SET cancelled_at = now() WHERE id = '{id}'"));

        exception.ConstraintName.ShouldBe("ck_subscriptions_cancellation");
    }

    [Fact]
    public async Task Delete_MemberWithASubscription_RejectedByTheForeignKey()
    {
        var memberId = await AddMemberAsync();
        await InsertAsync(memberId, Start, Start.AddDays(29));

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteAsync($"DELETE FROM members WHERE id = '{memberId}'"));

        // ON DELETE RESTRICT reports 23001 restrict_violation, not 23503.
        exception.SqlState.ShouldBe(PostgresErrorCodes.RestrictViolation);
    }

    [Fact]
    public async Task Save_StaleCopyAfterAnotherSave_ThrowsConcurrencyException()
    {
        var memberId = await AddMemberAsync();
        var id = await InsertAsync(memberId, Start, Start.AddDays(29));
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

    private async Task<Guid> AddMemberAsync(string phone = "+989121234567")
    {
        var member = TestMembers.Seed("رضا احمدی", phone);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member.Id;
    }

    /// <summary>One day, one session, flagged: what the API writes for a walk-in visit.</summary>
    private Task<Guid> InsertSingleVisitAsync(Guid memberId, DateOnly day) =>
        InsertAsync(memberId, day, day, totalSessions: 1, singleSession: true, durationDays: 1);

    private async Task<Guid> InsertAsync(
        Guid memberId, DateOnly start, DateOnly end, int? totalSessions = 10, int usedSessions = 0,
        bool cancelled = false, bool singleSession = false, int durationDays = 30)
    {
        var id = Guid.CreateVersion7();
        var cancelledAt = cancelled ? "now()" : "NULL";
        var reason = cancelled ? "'لغو'" : "NULL";
        var total = totalSessions?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "NULL";

        await ExecuteAsync(
            $"""
            INSERT INTO subscriptions (id, member_id, price, duration_days, total_sessions,
                                       start_date, end_date, used_sessions, total_frozen_days,
                                       cancelled_at, cancellation_reason, is_single_session, created_at)
            VALUES ('{id}', '{memberId}', 900000, {durationDays}, {total},
                    '{start:yyyy-MM-dd}', '{end:yyyy-MM-dd}', {usedSessions}, 0,
                    {cancelledAt}, {reason}, {(singleSession ? "TRUE" : "FALSE")}, now())
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
