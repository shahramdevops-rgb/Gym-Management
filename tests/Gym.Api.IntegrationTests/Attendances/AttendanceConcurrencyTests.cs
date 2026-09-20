using System.Linq.Expressions;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Lockers;
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
/// Task 5.4: the two races BUSINESS_RULES.md §7 names by name. Check-in has two different
/// safety nets — <c>LockMemberAsync</c> serializes requests for the same member so they never
/// reach the database constraint, while the free-locker pick has no lock at all and relies on
/// the partial unique index plus <see cref="CheckInHandler"/>'s catch block. These tests fire
/// real parallel requests at a live Postgres container to exercise both.
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
    public async Task CheckIn_ParallelForTheLastFreeLocker_NoLockerIsAssignedTwice()
    {
        var (staffClient, staffToken) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var plan = await AddPlanAsync();
        var locker = await CreateLockerAsync(ownerClient, ownerToken, 1);

        var members = new List<Member>();
        for (var i = 0; i < 6; i++)
        {
            var member = await AddMemberAsync();
            await AssignOkAsync(staffClient, staffToken, member.Id, plan.Id);
            members.Add(member);
        }

        // Six different members, so the member lock does not serialize them against each
        // other: only the partial unique index on locker_id (BUSINESS_RULES.md §7) stands
        // between this and two open attendances pointing at the same locker.
        var responses = await Task.WhenAll(members.Select(m => CheckInAsync(staffClient, staffToken, m.Id)));

        try
        {
            responses.Select(r => r.StatusCode)
                .ShouldAllBe(status => status == HttpStatusCode.Created || status == HttpStatusCode.Conflict);
            foreach (var response in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            {
                (await response.ReadErrorCodeAsync()).ShouldBe("Attendance.ChangedConcurrently");
            }
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        (await OpenAttendanceCountAsync(a => a.LockerId == locker.Id)).ShouldBeLessThanOrEqualTo(1);

        // Every member who was told they succeeded really did consume a session; a rejected
        // member's subscription was rolled back untouched and can retry.
        var created = responses.Count(r => r.StatusCode == HttpStatusCode.Created);
        (await OpenAttendanceCountAsync(a => members.Select(m => m.Id).Contains(a.MemberId))).ShouldBe(created);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private async Task<Member> AddMemberAsync()
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = Member.Create("رضا احمدی", $"+98913{suffix:D7}", null).Value;

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

    private static async Task<LockerResponse> CreateLockerAsync(HttpClient client, string token, int number)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, "/api/lockers", new { number });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LockerResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> CheckInAsync(HttpClient client, string token, Guid memberId) =>
        SendAsync(client, token, HttpMethod.Post, $"/api/members/{memberId}/attendance/check-in");

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
