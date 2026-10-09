using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Notifications.ListSmsMessages;
using Gym.Domain.Expenses;
using Gym.Domain.Notifications;
using Gym.Domain.Payables;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The SMS history: <c>GET /api/sms/messages</c> and <c>POST /api/sms/messages/{id}/resend</c>
/// (BUSINESS_RULES.md §10 <i>The SMS history</i>, task 10.5). Owner only.
/// </summary>
/// <remarks>
/// The messages are written straight into the table, their <c>created_at</c> then moved to a chosen
/// moment, since the API stamps rows with the real clock. The resend's outcomes and its sending
/// hours are in <see cref="ResendSmsTests"/>, on a clock pinned to Tehran's 10:00: here only the
/// refusals that do not depend on the time of day.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SmsMessagesEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Path = "/api/sms/messages";

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    /// <summary>1405/07/14.</summary>
    private static readonly DateOnly Day = new(2026, 10, 6);

    private static int _phoneSuffix;
    private static long _messageId;

    private string? _ownerToken;

    // ---- The list ----

    [Fact]
    public async Task List_Messages_NewestFirstWithTheMembersNameAndCostInToman()
    {
        var older = await AddBirthdayAsync("سارا محمدی", At(Day, 9, 0), sentCostRial: 1_350m);
        var newer = await AddBirthdayAsync("مینا کریمی", At(Day, 10, 0), failedCode: 424);

        var list = await ListAsync("");

        list.Items.Select(item => item.Id).ShouldBe([newer, older]);
        var sent = list.Items[1];
        sent.Kind.ShouldBe(NotificationKind.Birthday);
        sent.Status.ShouldBe(NotificationStatus.Sent);
        sent.MemberName.ShouldBe("سارا محمدی");
        sent.Text.ShouldBe(SmsText.ForBirthday("سارا محمدی", Day, Day));
        sent.CostToman.ShouldBe(135m);
        sent.Attempts.ShouldBe(1);
        var failed = list.Items[0];
        failed.Status.ShouldBe(NotificationStatus.Failed);
        failed.ErrorCode.ShouldBe(424);
        failed.CostToman.ShouldBeNull();
    }

    [Fact]
    public async Task List_PayableDue_IsTheOwnersWithNoMember()
    {
        var payableId = await AddPayableDueAsync(At(Day, 10, 0));

        var item = (await ListAsync("")).Items.ShouldHaveSingleItem();

        item.Kind.ShouldBe(NotificationKind.PayableDue);
        item.PayableId.ShouldBe(payableId);
        item.MemberId.ShouldBeNull();
        item.MemberName.ShouldBeNull();
    }

    [Fact]
    public async Task List_DateRange_UsesTheDayTheMessageWasWrittenInTehran()
    {
        // 20:40 UTC on the 5th is 00:10 on the 6th in Tehran; 20:20 UTC is still 23:50 on the 5th.
        var justAfterMidnight = await AddBirthdayAsync("سارا محمدی", new DateTimeOffset(2026, 10, 5, 20, 40, 0, TimeSpan.Zero));
        await AddBirthdayAsync("مینا کریمی", new DateTimeOffset(2026, 10, 5, 20, 20, 0, TimeSpan.Zero));
        var lastMinute = await AddBirthdayAsync("نگار احمدی", At(Day, 23, 59));
        await AddBirthdayAsync("لیلا رضایی", At(Day.AddDays(1), 0, 1));

        var list = await ListAsync("?from=2026-10-06&to=2026-10-06");

        list.Items.Select(item => item.Id).ShouldBe([lastMinute, justAfterMidnight]);
    }

    [Fact]
    public async Task List_KindAndStatus_OnlyTheMatchingOnes()
    {
        await AddBirthdayAsync("سارا محمدی", At(Day, 9, 0), sentCostRial: 1_350m);
        var failedBirthday = await AddBirthdayAsync("مینا کریمی", At(Day, 9, 5), failedCode: 424);
        await AddPayableDueAsync(At(Day, 9, 10), failedCode: 424);

        var list = await ListAsync("?kind=Birthday&status=Failed");

        list.Items.ShouldHaveSingleItem().Id.ShouldBe(failedBirthday);
        list.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task List_TotalCost_CountsEveryPageNotOnlyThisOne()
    {
        await AddBirthdayAsync("سارا محمدی", At(Day, 9, 0), sentCostRial: 1_350m);
        await AddBirthdayAsync("مینا کریمی", At(Day, 9, 5), sentCostRial: 2_700m);
        await AddBirthdayAsync("نگار احمدی", At(Day, 9, 10), failedCode: 418);

        var list = await ListAsync("?pageSize=1");

        list.Items.Count.ShouldBe(1);
        list.TotalCount.ShouldBe(3);
        list.TotalCostToman.ShouldBe(405m);
    }

    [Fact]
    public async Task List_TotalCost_IsOnlyTheFiltersMessages()
    {
        await AddBirthdayAsync("سارا محمدی", At(Day, 9, 0), sentCostRial: 1_350m);
        await AddBirthdayAsync("مینا کریمی", At(Day.AddDays(-30), 9, 0), sentCostRial: 2_700m);

        var list = await ListAsync("?from=2026-10-06&to=2026-10-06");

        list.TotalCostToman.ShouldBe(135m);
    }

    [Fact]
    public async Task List_FromAfterTo_Returns400WithTheCode()
    {
        var token = await OwnerTokenAsync();
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{Path}?from=2026-10-07&to=2026-10-06");

        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "to")).ShouldBe("Notifications.InvalidDateRange");
    }

    [Fact]
    public async Task List_AsStaff_Returns403()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        using var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);

        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Resend: the refusals that hold at any hour ----

    [Fact]
    public async Task Resend_Sent_Returns409AndSendsNothing()
    {
        var id = await AddBirthdayAsync("سارا محمدی", At(Day, 9, 0), sentCostRial: 1_350m);

        using var response = await ResendAsync(id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Notifications.NotResendable");
        (await ListAsync("")).Items.ShouldHaveSingleItem().Attempts.ShouldBe(1);
    }

    [Fact]
    public async Task Resend_UnknownId_Returns404()
    {
        using var response = await ResendAsync(Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Notifications.NotFound");
    }

    [Fact]
    public async Task Resend_AsStaff_Returns403()
    {
        var id = await AddBirthdayAsync("سارا محمدی", At(Day, 9, 0), failedCode: 424);
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        using var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Path}/{id}/resend");

        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Resend_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsync($"{Path}/{Guid.CreateVersion7()}/resend", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Helpers ----

    private async Task<SmsMessageListResponse> ListAsync(string query)
    {
        var token = await OwnerTokenAsync();
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Path + query);

        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<SmsMessageListResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private async Task<HttpResponseMessage> ResendAsync(Guid id)
    {
        var token = await OwnerTokenAsync();
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{Path}/{id}/resend");

        return await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private async Task<string> OwnerTokenAsync()
    {
        if (_ownerToken is null)
        {
            await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
            using var client = Fixture.CreateClient();
            _ownerToken = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);
        }

        return _ownerToken;
    }

    /// <summary>A birthday SMS for a new member, written at <paramref name="createdAt"/>; pending unless an outcome is given.</summary>
    private async Task<Guid> AddBirthdayAsync(
        string name, DateTimeOffset createdAt, decimal? sentCostRial = null, int? failedCode = null)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(name, $"+98913{suffix:D7}");
        var notification = Notification.ForBirthday(member.Id, 1405, member.PhoneNumber, SmsText.ForBirthday(name, Day, Day));
        Settle(notification, createdAt, sentCostRial, failedCode);

        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(member);
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await MoveCreatedAtAsync(notification.Id, createdAt);

        return notification.Id;
    }

    /// <summary>A cheque's SMS to the Owner, written at <paramref name="createdAt"/>.</summary>
    private async Task<Guid> AddPayableDueAsync(DateTimeOffset createdAt, int? failedCode = null)
    {
        var userId = (await TestUsers.CreateAsync(Fixture, userName: $"registrar{Interlocked.Increment(ref _phoneSuffix)}", role: Roles.Owner)).Id;
        var cheque = Payable.Register(
            PayableKind.Cheque, 12_500_000m, Day.AddDays(3), "فروشگاه تجهیزات ورزشی", "تردمیل",
            ExpenseCategory.CafePurchasingId, null, null, userId).Value;
        var notification = Notification.ForPayableDue(
            cheque.Id, "+989351112233", SmsText.ForPayableDue(cheque.Kind, cheque.Amount, cheque.DueDate, cheque.Payee));
        Settle(notification, createdAt, sentCostRial: null, failedCode);

        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Payables.Add(cheque);
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await MoveCreatedAtAsync(notification.Id, createdAt);

        return cheque.Id;
    }

    private static void Settle(Notification notification, DateTimeOffset at, decimal? sentCostRial, int? failedCode)
    {
        if (sentCostRial is { } cost)
        {
            notification.MarkSent(Interlocked.Increment(ref _messageId), cost, at);
        }
        else if (failedCode is { } code)
        {
            notification.MarkFailed(code, at);
        }
    }

    private async Task MoveCreatedAtAsync(Guid id, DateTimeOffset createdAt)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand("UPDATE notifications SET created_at = @at WHERE id = @id", connection);
        command.Parameters.AddWithValue("at", createdAt);
        command.Parameters.AddWithValue("id", id);
        (await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
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
