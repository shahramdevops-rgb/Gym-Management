using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Persistence.Seed;

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Performance;

/// <summary>
/// The "N+1" check of task 11.3: no screen's read endpoint sends more SQL commands for 25 rows
/// than for 1. A count that grows with the rows means a query per row, which only shows once the
/// gym has real data.
/// </summary>
/// <remarks>
/// <para>
/// "No more" rather than "the same": the audit list looks up names once per <i>kind</i> of record
/// on the page (a payment's member, a visit's member), so a page of 20 payments can need fewer
/// lookups than a page holding one record of every kind. That count is bounded by the kinds,
/// not by the rows.
/// </para>
/// <para>
/// One test walks every endpoint in one seeded gym rather than one test per endpoint: seeding
/// 25 members with a visit, a sale and a payment each is the slow part, and a single failure
/// message listing every endpoint that grows is more useful than a dozen separate ones.
/// </para>
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class QueryCountTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const int ManyRows = 25;

    private static readonly Guid ExpenseCategoryId = ExpenseCategorySeed.All[0].Id;

    [Fact]
    public async Task ReadEndpoints_TwentyFiveRowsInsteadOfOne_SendNoMoreQueries()
    {
        var counter = new QueryCounterHolder();
        await using var host = Fixture.CreateHost(services =>
        {
            services.AddSingleton(provider =>
                counter.Instance = new QueryCounter(provider.GetRequiredService<IHttpContextAccessor>()));
            services.ConfigureDbContext<AppDbContext>((provider, options) =>
                options.AddInterceptors(provider.GetRequiredService<QueryCounter>()));
        });
        using var client = DatabaseFixture.CreateClient(host);

        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var token = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);

        var plan = await TestPlans.AddAsync(Fixture);
        var productId = await ProductAsync(client, token);
        var first = await AddCustomerAsync(client, token, plan, productId, number: 1);

        var withOne = await MeasureAsync(client, token, counter, first);

        for (var number = 2; number <= ManyRows; number++)
        {
            await AddCustomerAsync(client, token, plan, productId, number);

            // The first member's own lists grow too: one more payment on their subscription.
            await PostAsync(
                client, token, $"/api/subscriptions/{first.SubscriptionId}/payments", new { amount = 10_000m, method = "Card" });
        }

        var withMany = await MeasureAsync(client, token, counter, first);

        var report = withOne.Keys
            .Select(path => $"{withOne[path],3} → {withMany[path],3}  {path}")
            .ToList();
        TestContext.Current.TestOutputHelper?.WriteLine(string.Join(Environment.NewLine, report));

        var growing = withOne.Keys.Where(path => withMany[path] > withOne[path]).ToList();
        growing.ShouldBeEmpty(
            "These endpoints send more queries for 25 rows than for 1 (queries with 1 → with 25):"
            + Environment.NewLine
            + string.Join(Environment.NewLine, growing.Select(path => $"{withOne[path]} → {withMany[path]}  {path}")));
    }

    /// <summary>Calls every read endpoint once and returns how many SQL commands each sent.</summary>
    private async Task<Dictionary<string, int>> MeasureAsync(
        HttpClient client, string token, QueryCounterHolder counter, Customer member)
    {
        var today = Iso(Today());
        var range = $"from={today}&to={today}";
        var lockerId = TestLockers.IdOf(1);

        string[] paths =
        [
            // The desk: the board, the member search, the member's page.
            "/api/attendance/currently-inside",
            "/api/attendance/today-by-hour",
            "/api/lockers?pageSize=100",
            $"/api/lockers/{lockerId}",
            $"/api/lockers/{lockerId}/today",
            "/api/members",
            "/api/members?search=عضو",
            "/api/members?debtorsOnly=true",
            $"/api/members/{member.MemberId}",
            $"/api/members/{member.MemberId}/debt",
            $"/api/members/{member.MemberId}/attendance",
            $"/api/members/{member.MemberId}/payments",
            $"/api/members/{member.MemberId}/subscriptions",
            $"/api/members/{member.MemberId}/cafe-orders",
            $"/api/subscriptions/{member.SubscriptionId}",
            "/api/cafe/orders",
            "/api/cafe/products",
            "/api/cafe/categories",
            "/api/auth/me",

            // The history pages.
            $"/api/attendance?{range}",
            $"/api/payments?{range}",
            $"/api/payments/totals?{range}",
            $"/api/service-charges?{range}",
            $"/api/sales?{range}",
            $"/api/sales/totals?{range}",

            // The Owner's pages.
            $"/api/reports/financial?{range}",
            "/api/reports/receivables",
            $"/api/reports/attendance?{range}",
            "/api/reports/subscriptions",
            $"/api/reports/cafe-products?{range}",
            "/api/reports/needs-attention",
            $"/api/reports/members?{range}",
            "/api/lockers/usage?days=30",
            $"/api/expenses?{range}",
            "/api/expenses/categories",
            "/api/payables",
            "/api/payables/due-soon",
            "/api/audit-logs",
            $"/api/audit-logs?memberId={member.MemberId}",
            "/api/audit-logs/users",
            "/api/sms/messages",
            "/api/sms/settings",
            "/api/staff",
            "/api/pricing",
        ];

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var key = Guid.CreateVersion7().ToString();
            var request = new HttpRequestMessage(HttpMethod.Get, path).WithBearer(token);
            request.Headers.Add(QueryCounter.HeaderName, key);

            using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, $"GET {path}");

            counts[path] = counter.Instance.ShouldNotBeNull().CountFor(key);
        }

        return counts;
    }

    /// <summary>
    /// One member as the desk would see them on a busy day: a plan half paid, inside with a
    /// locker, a cardio charge and an owed cafe order on the visit, a birthday SMS; plus the
    /// Owner's side: an expense, a cheque due in three days, and a guest on another locker.
    /// </summary>
    private async Task<Customer> AddCustomerAsync(
        HttpClient client, string token, TestPlan plan, Guid productId, int number)
    {
        var member = await AddMemberAsync(number);

        var subscription = await PostAsync(client, token, $"/api/members/{member.Id}/subscriptions", plan.Body);
        await PostAsync(client, token, $"/api/subscriptions/{subscription}/payments", new { amount = 450_000m, method = "Cash" });

        var visit = await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber: number);
        await PostAsync(client, token, $"/api/attendance/{visit.Id}/service-charges", new { kind = "Cardio", amount = 50_000m });
        await PostAsync(
            client,
            token,
            "/api/cafe/orders",
            new
            {
                memberId = member.Id,
                attendanceId = visit.Id,
                items = new[] { new { productId, quantity = 2 } },
                payment = (object?)null,
            });

        await PostAsync(
            client,
            token,
            "/api/expenses",
            new { amount = 100_000m, categoryId = ExpenseCategoryId, expenseDate = Today(), description = $"هزینه {number}" });
        await PostAsync(
            client,
            token,
            "/api/payables",
            new
            {
                kind = "Cheque",
                amount = 1_000_000m,
                dueDate = Today().AddDays(3),
                payee = $"فروشنده {number}",
                description = "چک",
                categoryId = ExpenseCategoryId,
            });
        await PostAsync(
            client, token, TestGuests.GuestCheckInPath, new { guestName = $"مهمان {number}", lockerId = TestLockers.IdOf(40 + number) });

        await AddBirthdaySmsAsync(member);

        return new Customer(member.Id, subscription);
    }

    private async Task<Member> AddMemberAsync(int number)
    {
        var member = TestMembers.Seed($"عضو شماره {number}", $"+98913{number:D7}");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>The SMS history is written by the jobs, not by an endpoint, so the row goes in directly.</summary>
    private async Task AddBirthdaySmsAsync(Member member)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.Add(Notification.ForBirthday(member.Id, jalaliYear: 1405, member.PhoneNumber, "تولدت مبارک"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> ProductAsync(HttpClient client, string token)
    {
        var categoryId = await PostAsync(client, token, "/api/cafe/categories", new { name = "نوشیدنی" });

        return await PostAsync(client, token, "/api/cafe/products", new { name = "آب معدنی", categoryId, price = 30_000m });
    }

    /// <summary>Posts, asserts success, and returns the <c>id</c> of what was created (empty when the answer has none).</summary>
    private static async Task<Guid> PostAsync(HttpClient client, string token, string path, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) }.WithBearer(token);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.ShouldBeTrue(
            $"POST {path} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)}");

        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return Guid.Empty;
        }

        var created = await response.Content.ReadFromJsonAsync<Created>(TestContext.Current.CancellationToken);

        return created?.Id ?? Guid.Empty;
    }

    private DateOnly Today() => Fixture.Services.GetRequiredService<IGymCalendar>().Today();

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private sealed record Customer(Guid MemberId, Guid SubscriptionId);

    private sealed record Created(Guid Id);

    /// <summary>
    /// The counter is created by the host's container, which is where the request accessor lives,
    /// so the test keeps a reference to it here when the container makes it.
    /// </summary>
    private sealed class QueryCounterHolder
    {
        public QueryCounter? Instance { get; set; }
    }
}
