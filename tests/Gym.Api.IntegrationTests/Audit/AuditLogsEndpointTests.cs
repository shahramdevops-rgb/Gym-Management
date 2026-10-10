using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Audit.ListAuditLogs;
using Gym.Application.Common;
using Gym.Application.Common.Paging;
using Gym.Domain.Attendances;
using Gym.Domain.Audit;
using Gym.Domain.Cafe;
using Gym.Domain.Notifications;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using NpgsqlTypes;

namespace Gym.Api.IntegrationTests.Audit;

/// <summary>
/// The audit screen: <c>GET /api/audit-logs</c> and <c>GET /api/audit-logs/users</c>
/// (BUSINESS_RULES.md §11 <i>The audit screen</i>, task 11.1). Owner only.
/// </summary>
/// <remarks>
/// Most rows are written straight into the table at a chosen moment on a day long past, since the
/// interceptor stamps its rows with the real clock and the table refuses an UPDATE. A filter on that
/// day leaves out the rows the test's own login writes. The member's rows are the real ones the
/// interceptor writes when the records are saved.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AuditLogsEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Path = "/api/audit-logs";

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    /// <summary>1403/12/20, well before any real clock this suite runs on.</summary>
    private static readonly DateOnly Day = new(2025, 3, 10);

    private const string DayFilter = "?from=2025-03-10&to=2025-03-10";

    private static int _phoneSuffix;

    private User? _owner;
    private string? _ownerToken;

    // ---- The list ----

    [Fact]
    public async Task List_Rows_NewestFirstWithWhoDidItAndTheChangedFields()
    {
        var owner = await OwnerAsync();
        var older = await AddRowAsync(At(Day, 9, 0), "Locker", userId: owner.Id, ipAddress: "10.0.0.5");
        var newer = await AddRowAsync(
            At(Day, 10, 0), "Subscription", userId: owner.Id,
            oldValues: """{"UsedSessions": 4}""", newValues: """{"UsedSessions": 5}""");

        var list = await ListAsync(DayFilter);

        list.Items.Select(item => item.Id).ShouldBe([newer, older]);
        var row = list.Items[0];
        row.Action.ShouldBe(AuditAction.Update);
        row.EntityType.ShouldBe("Subscription");
        row.UserId.ShouldBe(owner.Id);
        row.UserFullName.ShouldBe(owner.FullName);
        var change = row.Changes.ShouldHaveSingleItem();
        change.Field.ShouldBe("UsedSessions");
        change.OldValue!.Value.GetInt32().ShouldBe(4);
        change.NewValue!.Value.GetInt32().ShouldBe(5);
        list.Items[1].IpAddress.ShouldBe("10.0.0.5");
    }

    [Fact]
    public async Task List_Insert_HasOnlyNewValuesAndADeleteOnlyOldValues()
    {
        await AddRowAsync(At(Day, 9, 0), "Product", action: AuditAction.Insert, newValues: """{"Name": "آب معدنی", "Price": 20000}""");
        await AddRowAsync(At(Day, 9, 5), "TrustedDevice", action: AuditAction.Delete, oldValues: """{"FailedAttempts": 0}""");

        var list = await ListAsync(DayFilter + "&includeSignIns=true");

        var delete = list.Items[0];
        delete.Action.ShouldBe(AuditAction.Delete);
        var gone = delete.Changes.ShouldHaveSingleItem();
        gone.OldValue!.Value.GetInt32().ShouldBe(0);
        gone.NewValue.ShouldBeNull();
        var insert = list.Items[1];
        insert.Changes.Select(change => change.Field).Order().ShouldBe(["Name", "Price"]);
        insert.Changes.ShouldAllBe(change => change.OldValue == null);
    }

    [Fact]
    public async Task List_DateRange_UsesTheDayTheChangeHappenedInTehran()
    {
        // 20:40 UTC on the 9th is 00:10 on the 10th in Tehran; 20:20 UTC is still 23:50 on the 9th.
        var justAfterMidnight = await AddRowAsync(new DateTimeOffset(2025, 3, 9, 20, 40, 0, TimeSpan.Zero));
        await AddRowAsync(new DateTimeOffset(2025, 3, 9, 20, 20, 0, TimeSpan.Zero));
        var lastMinute = await AddRowAsync(At(Day, 23, 59));
        await AddRowAsync(At(Day.AddDays(1), 0, 1));

        var list = await ListAsync(DayFilter);

        list.Items.Select(item => item.Id).ShouldBe([lastMinute, justAfterMidnight]);
    }

    [Fact]
    public async Task List_SignIns_HiddenUnlessAskedFor()
    {
        var member = await AddRowAsync(At(Day, 9, 0), "Member");
        var token = await AddRowAsync(At(Day, 9, 1), "RefreshToken");
        var device = await AddRowAsync(At(Day, 9, 2), "TrustedDevice");

        (await ListAsync(DayFilter)).Items.Select(item => item.Id).ShouldBe([member]);
        (await ListAsync(DayFilter + "&includeSignIns=true")).Items.Select(item => item.Id).ShouldBe([device, token, member]);
    }

    [Fact]
    public async Task List_SignInKindChosen_ShowsItEvenWithTheSwitchOff()
    {
        await AddRowAsync(At(Day, 9, 0), "Member");
        var token = await AddRowAsync(At(Day, 9, 1), "RefreshToken");

        var list = await ListAsync(DayFilter + "&entityType=RefreshToken");

        list.Items.Select(item => item.Id).ShouldBe([token]);
    }

    [Fact]
    public async Task List_User_OnlyWhatThatUserDid()
    {
        var owner = await OwnerAsync();
        var staff = await TestUsers.CreateAsync(Fixture, userName: "reza");
        var byOwner = await AddRowAsync(At(Day, 9, 0), userId: owner.Id);
        await AddRowAsync(At(Day, 9, 1), userId: staff.Id);
        await AddRowAsync(At(Day, 9, 2));

        var list = await ListAsync(DayFilter + $"&userId={owner.Id}");

        list.Items.Select(item => item.Id).ShouldBe([byOwner]);
    }

    [Fact]
    public async Task List_SystemOnly_OnlyTheRowsWithNoUser()
    {
        var owner = await OwnerAsync();
        await AddRowAsync(At(Day, 9, 0), userId: owner.Id);
        var bySystem = await AddRowAsync(At(Day, 9, 1));

        var list = await ListAsync(DayFilter + "&systemOnly=true");

        var row = list.Items.ShouldHaveSingleItem();
        row.Id.ShouldBe(bySystem);
        row.UserId.ShouldBeNull();
        row.UserFullName.ShouldBeNull();
    }

    [Fact]
    public async Task List_EntityTypeAndId_OnlyThatRecordsHistory()
    {
        var recordId = Guid.CreateVersion7().ToString();
        var first = await AddRowAsync(At(Day, 9, 0), "Expense", recordId, AuditAction.Insert, newValues: """{"Amount": 500000}""");
        var second = await AddRowAsync(At(Day, 9, 5), "Expense", recordId);
        await AddRowAsync(At(Day, 9, 10), "Expense");
        await AddRowAsync(At(Day, 9, 15), "Payable", recordId);

        var list = await ListAsync(DayFilter + $"&entityType=Expense&entityId={recordId}");

        list.Items.Select(item => item.Id).ShouldBe([second, first]);
    }

    [Fact]
    public async Task List_Action_OnlyThatAction()
    {
        var insert = await AddRowAsync(At(Day, 9, 0), action: AuditAction.Insert, newValues: """{"FullName": "سارا محمدی"}""");
        await AddRowAsync(At(Day, 9, 5));

        var list = await ListAsync(DayFilter + "&action=Insert");

        list.Items.Select(item => item.Id).ShouldBe([insert]);
    }

    [Fact]
    public async Task List_Paging_TotalCountsEveryPageAndPagesNeverOverlap()
    {
        var first = await AddRowAsync(At(Day, 9, 0));
        var second = await AddRowAsync(At(Day, 9, 0));
        var third = await AddRowAsync(At(Day, 9, 0));

        var pageOne = await ListAsync(DayFilter + "&pageSize=2");
        var pageTwo = await ListAsync(DayFilter + "&pageSize=2&page=2");

        pageOne.TotalCount.ShouldBe(3);
        // The same moment for all three: the id, a version 7 Guid, keeps them apart and in order.
        pageOne.Items.Concat(pageTwo.Items).Select(item => item.Id).ShouldBe([third, second, first]);
    }

    [Fact]
    public async Task List_MemberFilter_FindsEveryRecordOfTheMemberAndNamesThem()
    {
        var owner = await OwnerAsync();
        var sara = await SeedMemberRecordsAsync("سارا محمدی", owner.Id, lockerNumber: 1);
        await SeedMemberRecordsAsync("مینا کریمی", owner.Id, lockerNumber: 2);

        var list = await ListAsync($"?memberId={sara}&pageSize=100");

        list.Items.Select(item => item.EntityType).Distinct().Order(StringComparer.Ordinal).ShouldBe(
        [
            "Attendance", "CafeOrder", "CafeOrderItem", "Member", "Notification", "Payment", "ServiceCharge", "Subscription",
        ]);
        list.Items.Count(item => item.EntityType == "Payment").ShouldBe(3, "one for the subscription, the charge and the order.");
        list.Items.ShouldAllBe(item => item.MemberId == sara && item.MemberFullName == "سارا محمدی");
    }

    [Fact]
    public async Task List_WithoutMemberFilter_NamesTheMemberOnlyWhereTheRecordHasOne()
    {
        var owner = await OwnerAsync();
        var sara = await SeedMemberRecordsAsync("سارا محمدی", owner.Id, lockerNumber: 1);

        var payments = await ListAsync("?entityType=Payment");
        var products = await ListAsync("?entityType=Product");

        payments.Items.Count.ShouldBe(3);
        payments.Items.ShouldAllBe(item => item.MemberId == sara && item.MemberFullName == "سارا محمدی");
        products.Items.ShouldHaveSingleItem().MemberId.ShouldBeNull();
    }

    [Fact]
    public async Task List_FromAfterTo_Returns400WithTheCode()
    {
        using var response = await GetAsync("?from=2025-03-11&to=2025-03-10");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "to")).ShouldBe("Audit.InvalidDateRange");
    }

    [Fact]
    public async Task List_UserAndSystemTogether_Returns400WithTheCode()
    {
        using var response = await GetAsync($"?userId={Guid.CreateVersion7()}&systemOnly=true");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "systemOnly")).ShouldBe("Audit.ConflictingUserFilter");
    }

    [Fact]
    public async Task List_AsStaff_Returns403()
    {
        using var response = await GetAsStaffAsync(Path);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_ReadsTheLogWithoutChangingIt()
    {
        await AddRowAsync(At(Day, 9, 0));
        // Logged in first: the login writes its own rows.
        await OwnerTokenAsync();
        var before = await CountRowsAsync();

        await ListAsync(DayFilter);

        // Reading writes nothing: the screen is not itself audited, and no row is changed.
        (await CountRowsAsync()).ShouldBe(before);
    }

    // ---- The users ----

    [Fact]
    public async Task Users_EveryAccountIncludingTheOwnerAndDeactivatedStaff()
    {
        var owner = await OwnerAsync();
        var staff = await TestUsers.CreateAsync(Fixture, userName: "reza");
        await TestUsers.DeactivateAsync(Fixture, staff.Id);

        using var response = await GetAsync("/users");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = (await response.Content.ReadFromJsonAsync<List<UserFullName>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        users.Select(user => user.Id).ShouldBe([owner.Id, staff.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Users_AsStaff_Returns403()
    {
        using var response = await GetAsStaffAsync($"{Path}/users");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- Helpers ----

    private async Task<PagedResponse<AuditLogResponse>> ListAsync(string query)
    {
        using var response = await GetAsync(query);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<PagedResponse<AuditLogResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private async Task<HttpResponseMessage> GetAsync(string pathAndQuery)
    {
        var token = await OwnerTokenAsync();
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Path + pathAndQuery);

        return await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private async Task<HttpResponseMessage> GetAsStaffAsync(string path)
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        using var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        return await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private async Task<User> OwnerAsync()
    {
        _owner ??= await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);

        return _owner;
    }

    private async Task<string> OwnerTokenAsync()
    {
        if (_ownerToken is null)
        {
            await OwnerAsync();
            using var client = Fixture.CreateClient();
            _ownerToken = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);
        }

        return _ownerToken;
    }

    /// <summary>
    /// An audit row written at <paramref name="occurredAt"/>, as the interceptor would write it. An
    /// update by default; an insert needs <paramref name="newValues"/> only and a delete
    /// <paramref name="oldValues"/> only, as the table's check constraint requires.
    /// </summary>
    private async Task<Guid> AddRowAsync(
        DateTimeOffset occurredAt,
        string entityType = "Member",
        string? entityId = null,
        AuditAction action = AuditAction.Update,
        Guid? userId = null,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null)
    {
        if (action == AuditAction.Update)
        {
            oldValues ??= """{"IsActive": true}""";
            newValues ??= """{"IsActive": false}""";
        }

        var id = Guid.CreateVersion7();

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO audit_logs (id, user_id, action, entity_type, entity_id, occurred_at, old_values, new_values, ip_address)
            VALUES (@id, @userId, @action, @entityType, @entityId, @occurredAt, @oldValues, @newValues, @ipAddress)
            """,
            connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.Add(new NpgsqlParameter("userId", NpgsqlDbType.Uuid) { Value = (object?)userId ?? DBNull.Value });
        command.Parameters.AddWithValue("action", action.ToString());
        command.Parameters.AddWithValue("entityType", entityType);
        command.Parameters.AddWithValue("entityId", entityId ?? Guid.CreateVersion7().ToString());
        command.Parameters.AddWithValue("occurredAt", occurredAt);
        command.Parameters.Add(new NpgsqlParameter("oldValues", NpgsqlDbType.Jsonb) { Value = (object?)oldValues ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("newValues", NpgsqlDbType.Jsonb) { Value = (object?)newValues ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("ipAddress", NpgsqlDbType.Varchar) { Value = (object?)ipAddress ?? DBNull.Value });
        (await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken)).ShouldBe(1);

        return id;
    }

    /// <summary>
    /// A member with one of each record that belongs to a member, saved through EF Core so the
    /// interceptor writes their audit rows: a subscription, a visit, a هوازی charge, a cafe order of
    /// one line, a payment for each of the three, and a birthday SMS.
    /// </summary>
    private async Task<Guid> SeedMemberRecordsAsync(string fullName, Guid userId, int lockerNumber)
    {
        var now = At(Day, 10, 0);
        var member = TestMembers.Seed(fullName, $"+98912{Interlocked.Increment(ref _phoneSuffix):D7}");
        var subscription = Subscription.CreateMembership(member.Id, 12, 10_000m, Day).Value;
        var attendance = Attendance.CheckIn(member.Id, subscription.Id, TestLockers.IdOf(lockerNumber), now);
        var charge = ServiceCharge.Record(member.Id, attendance.Id, ServiceChargeKind.Cardio, 50_000m, Day, userId).Value;
        var category = ProductCategory.Create($"نوشیدنی {lockerNumber}").Value;
        var product = Product.Create($"آب معدنی {lockerNumber}", category.Id, 20_000m).Value;
        var order = CafeOrder.Create(member.Id, [(product, 2)], Day, userId).Value;
        var payments = new[]
        {
            Payment.RegisterForSubscription(subscription.Id, 100_000m, PaymentMethod.Cash, null, userId, now).Value,
            Payment.RegisterForServiceCharge(charge.Id, 50_000m, PaymentMethod.Card, null, userId, now).Value,
            Payment.RegisterForCafeOrder(order.Id, 40_000m, PaymentMethod.Cash, null, userId, now).Value,
        };
        var notification = Notification.ForBirthday(member.Id, 1403, member.PhoneNumber, $"{fullName} عزیز، تولدت مبارک");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        db.Subscriptions.Add(subscription);
        db.Attendances.Add(attendance);
        db.ServiceCharges.Add(charge);
        db.ProductCategories.Add(category);
        db.Products.Add(product);
        db.CafeOrders.Add(order);
        db.Payments.AddRange(payments);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member.Id;
    }

    private async Task<long> CountRowsAsync()
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM audit_logs", connection);

        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<string?> FieldErrorCodeAsync(HttpResponseMessage response, string field)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString();
    }

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }
}
