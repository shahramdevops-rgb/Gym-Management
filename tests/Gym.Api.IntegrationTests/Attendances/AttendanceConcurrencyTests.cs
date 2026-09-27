using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Domain.Attendances;
using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Attendances;

/// <summary>
/// Task 5.4, updated for 6.5.5: the races BUSINESS_RULES.md §6 and §7 name. Check-in has different
/// safety nets — <c>LockMemberAsync</c> serializes requests for the same member so they never
/// reach the database constraint, while two desks choosing one locker or one reserve place have
/// no lock at all and rely on the partial unique indexes plus <see cref="CheckInHandler"/>'s catch
/// blocks; a move relies on the visit's <c>xmin</c>. These tests fire real parallel requests at a
/// live Postgres container, or make the stale read on purpose.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AttendanceConcurrencyTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private static int _phoneSuffix;

    [Fact]
    public async Task CheckIn_ParallelForTheSameMember_OnlyOneSucceedsAndOneSessionIsConsumed()
    {
        var (client, token) = await StaffClientAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanAsync();
        await AssignOkAsync(client, token, member.Id, plan.Id);

        // The member row lock makes these take turns: whichever gets there second sees the
        // first one's committed attendance and is rejected as a normal business rule, not a
        // database conflict (BUSINESS_RULES.md §7; the same reasoning as the existing
        // CheckIn_AlreadyCheckedIn_Returns422AttendanceAlreadyCheckedIn test, now under real
        // concurrency instead of two awaited calls in sequence).
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CheckInAsync(client, token, member.Id)));

        try
        {
            responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            responses.Count(r => r.StatusCode == HttpStatusCode.UnprocessableEntity).ShouldBe(5);
            foreach (var response in responses.Where(r => r.StatusCode == HttpStatusCode.UnprocessableEntity))
            {
                (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.AlreadyCheckedIn");
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        (await StoredSubscriptionAsync(member.Id)).UsedSessions.ShouldBe(1);
        (await OpenAttendanceCountAsync(a => a.MemberId == member.Id)).ShouldBe(1);
    }

    [Fact]
    public async Task CheckIn_ParallelForTheSameLocker_OneWinsAndTheRestGetLockerTaken()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var plan = await AddPlanAsync();
        var members = await MembersWithAPlanAsync(staffClient, staffToken, plan.Id, count: 6);

        // Six desks click locker 1 for six different members at once, so the member lock does not
        // serialize them: the read in LockerChoice turns some away, and the partial unique index on
        // locker_id (BUSINESS_RULES.md §7) settles the ones that raced past it. Either way the desk
        // hears the same thing — someone holds that locker, choose another.
        var responses = await Task.WhenAll(
            members.Select(m => TestLockers.CheckInAsync(staffClient, staffToken, m.Id, lockerNumber: 1)));

        try
        {
            responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(5);
            foreach (var response in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            {
                (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.LockerTaken");
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        (await OpenAttendanceCountAsync(a => a.LockerId == TestLockers.IdOf(1))).ShouldBe(1);

        // A refused member's session was rolled back with the rest of their transaction.
        (await OpenAttendanceCountAsync(a => members.Select(m => m.Id).Contains(a.MemberId))).ShouldBe(1);
    }

    [Fact]
    public async Task CheckIn_ParallelForReservePlaces_NoPlaceIsHeldTwiceAndNeverMoreThanFifteen()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var plan = await AddPlanAsync();
        await TestLockers.TakeOutOfServiceAllButAsync(Fixture);
        var members = await MembersWithAPlanAsync(staffClient, staffToken, plan.Id, count: Attendance.ReservePlaceCount + 3);

        // Every request reads the free places before any commits, so most pick place 1; the
        // partial unique index on reserve_slot turns the losers away with "try again".
        var responses = await Task.WhenAll(
            members.Select(m => TestLockers.CheckInOnReservePlaceAsync(staffClient, staffToken, m.Id)));

        try
        {
            foreach (var response in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
            {
                (await response.ReadErrorCodeAsync()).ShouldBeOneOf("Attendance.ChangedConcurrently", "Attendance.ReserveFull");
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        var slots = await OpenReserveSlotsAsync();
        slots.Count.ShouldBe(responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        slots.Count.ShouldBeLessThanOrEqualTo(Attendance.ReservePlaceCount);
        slots.ShouldBeUnique();
    }

    /// <summary>
    /// The race a move can lose: it read the visit while open, and another desk checked it out
    /// before the move saved. A timed race cannot tell the bad outcome from a good one (both calls
    /// "succeed" either way), so the stale read is made on purpose and the save must be refused by
    /// the visit's <c>xmin</c> rather than put a closed visit on another locker.
    /// </summary>
    [Fact]
    public async Task Attendance_MovedFromAReadTakenBeforeCheckOut_RefusedByTheVersion()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var plan = await AddPlanAsync();
        var member = (await MembersWithAPlanAsync(staffClient, staffToken, plan.Id, count: 1)).Single();
        var visit = await TestLockers.CheckInOkAsync(staffClient, staffToken, member.Id, lockerNumber: 1);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stale = await db.Attendances.SingleAsync(a => a.Id == visit.Id, TestContext.Current.CancellationToken);
        using (var checkedOut = await SendAsync(staffClient, staffToken, HttpMethod.Post, $"/api/attendance/{visit.Id}/check-out"))
        {
            checkedOut.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        stale.MoveToLocker(TestLockers.IdOf(2)).IsSuccess.ShouldBeTrue("the stale copy still looks open.");

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        (await StoredAttendanceAsync(visit.Id)).LockerId.ShouldBe(TestLockers.IdOf(1));
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed("رضا احمدی", $"+98913{suffix:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task<Plan> AddPlanAsync()
    {
        var plan = Plan.Create("پلن", 30, 12, 900_000m).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Plans.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return plan;
    }

    private async Task<Subscription> StoredSubscriptionAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Subscriptions
            .AsNoTracking()
            .SingleAsync(s => s.MemberId == memberId, TestContext.Current.CancellationToken);
    }

    private async Task<int> OpenAttendanceCountAsync(Expression<Func<Attendance, bool>> predicate)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attendances
            .AsNoTracking()
            .Where(a => a.CheckedOutAt == null)
            .Where(predicate)
            .CountAsync(TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> AssignAsync(HttpClient client, string token, Guid memberId, Guid planId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/subscriptions", new { planId });

    private static async Task AssignOkAsync(HttpClient client, string token, Guid memberId, Guid planId)
    {
        using var response = await AssignAsync(client, token, memberId, planId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<List<Member>> MembersWithAPlanAsync(HttpClient client, string token, Guid planId, int count)
    {
        var members = new List<Member>();
        for (var i = 0; i < count; i++)
        {
            var member = await AddMemberAsync();
            await AssignOkAsync(client, token, member.Id, planId);
            members.Add(member);
        }

        return members;
    }

    private async Task<List<int>> OpenReserveSlotsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attendances
            .AsNoTracking()
            .Where(a => a.CheckedOutAt == null && a.ReserveSlot != null)
            .Select(a => a.ReserveSlot!.Value)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Attendance> StoredAttendanceAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Attendances
            .AsNoTracking()
            .SingleAsync(a => a.Id == id, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> CheckInAsync(HttpClient client, string token, Guid memberId) =>
        TestLockers.CheckInAsync(client, token, memberId);

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }
}
