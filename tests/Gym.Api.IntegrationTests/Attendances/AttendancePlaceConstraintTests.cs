using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Domain.Plans;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary>
/// The database's own copy of the locker and reserve-place rules (BUSINESS_RULES.md §6), tried
/// with raw SQL that goes around the application, the way a bug or a hand-typed fix would.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AttendancePlaceConstraintTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Theory]
    [InlineData(0)]
    [InlineData(16)]
    public async Task Attendance_ReserveSlotOutOfRange_RejectedByACheckConstraint(int slot)
    {
        var visitId = await OpenVisitAsync(lockerNumber: 1);

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE attendances SET locker_id = NULL, reserve_slot = {slot} WHERE id = '{visitId}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe(AttendanceConstraints.ReserveSlotRange);
    }

    [Fact]
    public async Task Attendance_OpenVisitWithBothALockerAndAReservePlace_RejectedByACheckConstraint()
    {
        var visitId = await OpenVisitAsync(lockerNumber: 1);

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE attendances SET reserve_slot = 1 WHERE id = '{visitId}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe(AttendanceConstraints.OpenHoldsOnePlace);
    }

    [Fact]
    public async Task Attendance_OpenVisitWithNeither_RejectedByACheckConstraint()
    {
        var visitId = await OpenVisitAsync(lockerNumber: 1);

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE attendances SET locker_id = NULL WHERE id = '{visitId}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe(AttendanceConstraints.OpenHoldsOnePlace);
    }

    [Fact]
    public async Task Attendance_ClosedVisitWithNeither_IsAllowed()
    {
        // A visit closed before 6.5.5 with no locker free had neither, and must stay storable.
        var visitId = await OpenVisitAsync(lockerNumber: 1);
        await ExecuteSqlAsync($"UPDATE attendances SET checked_out_at = now() WHERE id = '{visitId}'");

        await ExecuteSqlAsync($"UPDATE attendances SET locker_id = NULL WHERE id = '{visitId}'");
    }

    [Fact]
    public async Task Attendance_TwoOpenVisitsOnOneReservePlace_RejectedByThePartialUniqueIndex()
    {
        var first = await OpenVisitAsync(lockerNumber: 1);
        var second = await OpenVisitAsync(lockerNumber: 2);
        await ExecuteSqlAsync($"UPDATE attendances SET locker_id = NULL, reserve_slot = 3 WHERE id = '{first}'");

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE attendances SET locker_id = NULL, reserve_slot = 3 WHERE id = '{second}'"));

        exception.SqlState.ShouldBe("23505");
        exception.ConstraintName.ShouldBe(AttendanceConstraints.OneOpenPerReserveSlot);
    }

    [Fact]
    public async Task Attendance_TwoOpenVisitsOnOneLocker_RejectedByThePartialUniqueIndex()
    {
        await OpenVisitAsync(lockerNumber: 1);
        var second = await OpenVisitAsync(lockerNumber: 2);

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE attendances SET locker_id = '{TestLockers.IdOf(1)}' WHERE id = '{second}'"));

        exception.SqlState.ShouldBe("23505");
        exception.ConstraintName.ShouldBe(AttendanceConstraints.OneOpenPerLocker);
    }

    // ---- Helpers ----

    /// <summary>A real check-in through the API, so every other column is exactly what the app writes.</summary>
    private async Task<Guid> OpenVisitAsync(int lockerNumber)
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: $"staff{lockerNumber}", role: Roles.Staff);
        var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync($"staff{lockerNumber}", TestUsers.Password);

        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98915{suffix:D7}");
        var plan = Plan.Create($"پلن {suffix}", 30, 12, 900_000m).Value;
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(member);
            db.Plans.Add(plan);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/members/{member.Id}/subscriptions")
        {
            Content = JsonContent.Create(new { planId = plan.Id }),
        };
        using (var assigned = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken))
        {
            assigned.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        return (await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber)).Id;
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
