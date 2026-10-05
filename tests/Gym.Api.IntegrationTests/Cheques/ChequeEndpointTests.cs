using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Cheques;
using Gym.Application.Cheques.ListCheques;
using Gym.Application.Common;
using Gym.Application.Reports.GetNeedsAttention;
using Gym.Domain.Audit;
using Gym.Domain.Cheques;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Cheques;

/// <summary>
/// <c>/api/cheques</c> and the dashboard's «چک‌های نزدیک سررسید». BUSINESS_RULES.md §9
/// <i>Cheques</i>: registered, edited while pending, then passed (on or after its date) or cancelled
/// with a reason, both final, never deleted. §1: Owner only, reading included.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ChequeEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string ChequesPath = "/api/cheques";
    private const string NeedsAttentionPath = "/api/reports/needs-attention";

    // ---- Register ----

    [Fact]
    public async Task RegisterCheque_AsOwner_Returns201WithEveryField()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var dueDate = (await TodayAsync()).AddDays(20);

        using var response = await RegisterAsync(client, owner, 50_000_000m, dueDate, "  فروشگاه تجهیزات ", " قسط دوم تردمیل ");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var cheque = (await response.Content.ReadFromJsonAsync<ChequeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{ChequesPath}/{cheque.Id}");
        cheque.Amount.ShouldBe(50_000_000m);
        cheque.DueDate.ShouldBe(dueDate);
        cheque.Payee.ShouldBe("فروشگاه تجهیزات");
        cheque.Description.ShouldBe("قسط دوم تردمیل");
        cheque.Status.ShouldBe(ChequeStatus.Pending);
        cheque.RegisteredByUserId.ShouldBe(ownerId);
        cheque.PassedAt.ShouldBeNull();
        cheque.CancelledAt.ShouldBeNull();
    }

    [Fact]
    public async Task RegisterCheque_StatusIsSentAsItsName()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RegisterAsync(client, owner, 1_000m, await TodayAsync(), "الف", "ب");

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("status").GetString().ShouldBe("Pending");
    }

    [Fact]
    public async Task RegisterCheque_AsStaff_Returns403AndRegistersNothing()
    {
        var (client, _, staff, _) = await ClientsAsync();

        using var response = await RegisterAsync(client, staff, 1_000m, await TodayAsync(), "الف", "ب");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CountChequesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RegisterCheque_DatedLastYear_Returns201()
    {
        // BUSINESS_RULES.md §9 Cheques: an old cheque can still be entered late.
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RegisterAsync(client, owner, 1_000m, (await TodayAsync()).AddYears(-1), "الف", "ب");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("0", "الف", "ب", "amount", "Cheques.AmountNotPositive")]
    [InlineData("1000.001", "الف", "ب", "amount", "Cheques.AmountTooManyDecimals")]
    [InlineData("1000", "  ", "ب", "payee", "Cheques.PayeeRequired")]
    [InlineData("1000", "الف", "  ", "description", "Cheques.DescriptionRequired")]
    public async Task RegisterCheque_InvalidField_Returns400WithFieldCode(
        string amount, string payee, string description, string field, string code)
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RegisterAsync(
            client, owner, decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
            await TodayAsync(), payee, description);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, field)).ShouldBe(code);
    }

    [Fact]
    public async Task RegisterCheque_PayeeOverTheLimit_Returns400()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RegisterAsync(
            client, owner, 1_000m, await TodayAsync(), new string('ب', Cheque.PayeeMaxLength + 1), "ب");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "payee")).ShouldBe("Cheques.PayeeTooLong");
    }

    [Fact]
    public async Task RegisterCheque_AmountWithTwoDecimals_IsStoredExactly()
    {
        var (client, owner, _, _) = await ClientsAsync();

        var cheque = await RegisterChequeAsync(client, owner, 1_234_567.89m, await TodayAsync());

        (await StoredAsync(cheque.Id)).Amount.ShouldBe(1_234_567.89m);
    }

    // ---- Get ----

    [Fact]
    public async Task GetCheque_AsOwner_ReturnsIt()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{ChequesPath}/{cheque.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ChequeResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().Id.ShouldBe(cheque.Id);
    }

    [Fact]
    public async Task GetCheque_AsStaff_Returns403()
    {
        var (client, owner, staff, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await SendAsync(client, staff, HttpMethod.Get, $"{ChequesPath}/{cheque.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCheque_UnknownId_Returns404()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{ChequesPath}/{Guid.CreateVersion7()}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Cheques.NotFound");
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateCheque_AsOwner_ReplacesTheFields()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);

        using var response = await UpdateAsync(client, owner, cheque.Id, 2_500m, today.AddDays(30), "پرداخت‌گیرنده", "قسط سوم", cheque.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await StoredAsync(cheque.Id);
        stored.Amount.ShouldBe(2_500m);
        stored.DueDate.ShouldBe(today.AddDays(30));
        stored.Payee.ShouldBe("پرداخت‌گیرنده");
        stored.Description.ShouldBe("قسط سوم");
    }

    [Fact]
    public async Task UpdateCheque_AsOwner_WritesTheOldAndNewAmountToTheAuditLog()
    {
        // BUSINESS_RULES.md §9 Cheques: every edit is audited.
        var (client, owner, _, ownerId) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);

        (await UpdateAsync(client, owner, cheque.Id, 2_500m, today, "الف", "ب", cheque.Version)).EnsureSuccessStatusCode().Dispose();

        await using var scope = Fixture.CreateScope();
        var update = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .SingleAsync(
                log => log.EntityType == nameof(Cheque) && log.EntityId == cheque.Id.ToString() && log.Action == AuditAction.Update,
                TestContext.Current.CancellationToken);
        update.UserId.ShouldBe(ownerId);
        Value(update.OldValues, nameof(Cheque.Amount)).GetDecimal().ShouldBe(1_000m);
        Value(update.NewValues, nameof(Cheque.Amount)).GetDecimal().ShouldBe(2_500m);
    }

    [Fact]
    public async Task UpdateCheque_AsStaff_Returns403()
    {
        var (client, owner, staff, _) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);

        using var response = await UpdateAsync(client, staff, cheque.Id, 2_500m, today, "الف", "ب", cheque.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await StoredAsync(cheque.Id)).Amount.ShouldBe(1_000m);
    }

    [Fact]
    public async Task UpdateCheque_StaleVersion_Returns409AndKeepsTheNewerEdit()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);
        (await UpdateAsync(client, owner, cheque.Id, 2_000m, today, "الف", "ب", cheque.Version)).Dispose();

        using var response = await UpdateAsync(client, owner, cheque.Id, 3_000m, today, "الف", "ب", cheque.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Cheques.ChangedConcurrently");
        (await StoredAsync(cheque.Id)).Amount.ShouldBe(2_000m);
    }

    [Fact]
    public async Task UpdateCheque_Passed_Returns422AndChangesNothing()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);
        var passed = await PassChequeAsync(client, owner, cheque.Id);

        using var response = await UpdateAsync(client, owner, cheque.Id, 2_000m, today, "الف", "ب", passed.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Cheques.AlreadyPassed");
        (await StoredAsync(cheque.Id)).Amount.ShouldBe(1_000m);
    }

    [Fact]
    public async Task UpdateCheque_Cancelled_Returns422AndChangesNothing()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);
        var cancelled = await CancelChequeAsync(client, owner, cheque.Id);

        using var response = await UpdateAsync(client, owner, cheque.Id, 2_000m, today, "الف", "ب", cancelled.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Cheques.AlreadyCancelled");
    }

    // ---- Pass ----

    [Fact]
    public async Task PassCheque_OnItsDate_MarksItPassedWithTheMomentAndTheUser()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await PassAsync(client, owner, cheque.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var passed = (await response.Content.ReadFromJsonAsync<ChequeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        passed.Status.ShouldBe(ChequeStatus.Passed);
        passed.PassedAt.ShouldNotBeNull();
        passed.PassedByUserId.ShouldBe(ownerId);
    }

    [Fact]
    public async Task PassCheque_Tomorrow_Returns422NotDueYetAndStaysPending()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, (await TodayAsync()).AddDays(1));

        using var response = await PassAsync(client, owner, cheque.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Cheques.NotDueYet");
        (await StoredAsync(cheque.Id)).IsPending.ShouldBeTrue();
    }

    [Fact]
    public async Task PassCheque_Twice_Returns422AlreadyPassed()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await PassChequeAsync(client, owner, cheque.Id);

        using var response = await PassAsync(client, owner, cheque.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Cheques.AlreadyPassed");
    }

    [Fact]
    public async Task PassCheque_AsStaff_Returns403()
    {
        var (client, owner, staff, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await PassAsync(client, staff, cheque.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await StoredAsync(cheque.Id)).IsPending.ShouldBeTrue();
    }

    [Fact]
    public async Task PassCheque_UnknownId_Returns404()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await PassAsync(client, owner, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PassAndCancel_InParallel_ExactlyOneSucceeds()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        var responses = await Task.WhenAll(
            PassAsync(client, owner, cheque.Id),
            CancelAsync(client, owner, cheque.Id, "پس گرفته شد"),
            PassAsync(client, owner, cheque.Id),
            CancelAsync(client, owner, cheque.Id, "اشتباه"));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            responses.ShouldAllBe(response =>
                response.StatusCode == HttpStatusCode.OK ||
                response.StatusCode == HttpStatusCode.Conflict ||
                response.StatusCode == HttpStatusCode.UnprocessableEntity);
            var stored = await StoredAsync(cheque.Id);
            (stored.IsPassed ^ stored.IsCancelled).ShouldBeTrue();
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    // ---- Cancel ----

    [Fact]
    public async Task CancelCheque_AsOwner_KeepsTheRowMarkedWithTheReason()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, (await TodayAsync()).AddDays(10));

        var cancelled = await CancelChequeAsync(client, owner, cheque.Id, "  از فروشنده پس گرفته شد ");

        cancelled.Status.ShouldBe(ChequeStatus.Cancelled);
        cancelled.CancelReason.ShouldBe("از فروشنده پس گرفته شد");
        cancelled.CancelledByUserId.ShouldBe(ownerId);
        (await CountChequesAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CancelCheque_Twice_Returns422AndKeepsTheFirstReason()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await CancelChequeAsync(client, owner, cheque.Id, "اول");

        using var response = await CancelAsync(client, owner, cheque.Id, "دوم");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Cheques.AlreadyCancelled");
        (await StoredAsync(cheque.Id)).CancelReason.ShouldBe("اول");
    }

    [Fact]
    public async Task CancelCheque_Passed_Returns422AlreadyPassed()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await PassChequeAsync(client, owner, cheque.Id);

        using var response = await CancelAsync(client, owner, cheque.Id, "دیر شد");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Cheques.AlreadyPassed");
    }

    [Fact]
    public async Task CancelCheque_BlankReason_Returns400()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await CancelAsync(client, owner, cheque.Id, "   ");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "reason")).ShouldBe("Cheques.CancelReasonRequired");
    }

    [Fact]
    public async Task CancelCheque_AsStaff_Returns403()
    {
        var (client, owner, staff, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await CancelAsync(client, staff, cheque.Id, "اشتباه");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await StoredAsync(cheque.Id)).IsPending.ShouldBeTrue();
    }

    [Fact]
    public async Task DeleteCheque_AsOwner_HasNoEndpoint()
    {
        // BUSINESS_RULES.md §9 Cheques: cancelled with a reason, never deleted.
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await SendAsync(client, owner, HttpMethod.Delete, $"{ChequesPath}/{cheque.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await CountChequesAsync()).ShouldBe(1);
    }

    // ---- List ----

    [Fact]
    public async Task ListCheques_Pending_EarliestDateFirstWithThePendingTotal()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var later = await RegisterChequeAsync(client, owner, 3_000m, today.AddDays(40));
        var overdue = await RegisterChequeAsync(client, owner, 2_000m, today.AddDays(-3));
        var passed = await RegisterChequeAsync(client, owner, 10_000m, today.AddDays(-1));
        await PassChequeAsync(client, owner, passed.Id);
        var cancelled = await RegisterChequeAsync(client, owner, 20_000m, today.AddDays(5));
        await CancelChequeAsync(client, owner, cancelled.Id);

        var list = await ListAsync(client, owner, "status=Pending");

        list.Items.Select(cheque => cheque.Id).ShouldBe([overdue.Id, later.Id]);
        list.TotalCount.ShouldBe(2);
        list.PendingTotal.ShouldBe(5_000m);
    }

    [Theory]
    [InlineData("status=Passed", ChequeStatus.Passed)]
    [InlineData("status=Cancelled", ChequeStatus.Cancelled)]
    public async Task ListCheques_ByStatus_ListsOnlyThatStatusAndKeepsThePendingTotal(string query, ChequeStatus status)
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        await RegisterChequeAsync(client, owner, 3_000m, today.AddDays(40));
        var passed = await RegisterChequeAsync(client, owner, 10_000m, today);
        await PassChequeAsync(client, owner, passed.Id);
        var cancelled = await RegisterChequeAsync(client, owner, 20_000m, today);
        await CancelChequeAsync(client, owner, cancelled.Id);

        var list = await ListAsync(client, owner, query);

        list.Items.ShouldHaveSingleItem().Status.ShouldBe(status);
        list.PendingTotal.ShouldBe(3_000m);
    }

    [Fact]
    public async Task ListCheques_NoStatus_ListsEveryChequeLatestDateFirst()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var early = await RegisterChequeAsync(client, owner, 1_000m, today.AddDays(-10));
        var late = await RegisterChequeAsync(client, owner, 1_000m, today.AddDays(10));
        var middle = await RegisterChequeAsync(client, owner, 1_000m, today);
        await CancelChequeAsync(client, owner, middle.Id);

        var list = await ListAsync(client, owner);

        list.Items.Select(cheque => cheque.Id).ShouldBe([late.Id, middle.Id, early.Id]);
    }

    [Fact]
    public async Task ListCheques_PendingTotalCoversEveryPageNotJustTheFirst()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        for (var i = 0; i < 3; i++)
        {
            await RegisterChequeAsync(client, owner, 1_000m, today.AddDays(i));
        }

        var list = await ListAsync(client, owner, "status=Pending&pageSize=2");

        list.Items.Count.ShouldBe(2);
        list.TotalCount.ShouldBe(3);
        list.PendingTotal.ShouldBe(3_000m);
    }

    [Fact]
    public async Task ListCheques_UnknownStatus_Returns400()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{ChequesPath}?status=Bounced", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListCheques_AsStaff_Returns403()
    {
        var (client, _, staff, _) = await ClientsAsync();

        using var response = await SendAsync(client, staff, HttpMethod.Get, ChequesPath, body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- The dashboard's reminder ----

    [Fact]
    public async Task NeedsAttention_Cheques_ListsPendingWithinSevenDaysAndPastTheirDate()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var overdue = await RegisterChequeAsync(client, owner, 1_000m, today.AddDays(-15), "تأخیری");
        var dueToday = await RegisterChequeAsync(client, owner, 2_000m, today, "امروز");
        var daySeven = await RegisterChequeAsync(client, owner, 3_000m, today.AddDays(7), "روز هفتم");
        await RegisterChequeAsync(client, owner, 4_000m, today.AddDays(8), "روز هشتم");
        var passed = await RegisterChequeAsync(client, owner, 5_000m, today.AddDays(-1), "پاس شده");
        await PassChequeAsync(client, owner, passed.Id);
        var cancelled = await RegisterChequeAsync(client, owner, 6_000m, today.AddDays(2), "باطل");
        await CancelChequeAsync(client, owner, cancelled.Id);

        using var response = await SendAsync(client, owner, HttpMethod.Get, NeedsAttentionPath, body: null);
        response.EnsureSuccessStatusCode();
        var needs = (await response.Content.ReadFromJsonAsync<NeedsAttentionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

        // Day 7 is in and day 8 is out; past its date stays until marked; the earliest first.
        needs.ChequesDue.ShouldBe(
        [
            new ChequeDueResponse(overdue.Id, "تأخیری", 1_000m, today.AddDays(-15), "قسط"),
            new ChequeDueResponse(dueToday.Id, "امروز", 2_000m, today, "قسط"),
            new ChequeDueResponse(daySeven.Id, "روز هفتم", 3_000m, today.AddDays(7), "قسط"),
        ]);
    }

    // ---- The database's own copy of the rules ----

    [Fact]
    public async Task Cheque_PassedAndCancelled_RejectedByACheckConstraint()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await PassChequeAsync(client, owner, cheque.Id);

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE cheques SET cancelled_at = now(), cancel_reason = 'x', cancelled_by_user_id = '{ownerId}' WHERE id = '{cheque.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe("ck_cheques_not_passed_and_cancelled");
    }

    [Fact]
    public async Task Cheque_CancelledWithoutAReason_RejectedByACheckConstraint()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE cheques SET cancelled_at = now(), cancelled_by_user_id = '{ownerId}' WHERE id = '{cheque.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe("ck_cheques_cancelled");
    }

    [Fact]
    public async Task Cheque_PassedWithoutAUser_RejectedByACheckConstraint()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE cheques SET passed_at = now() WHERE id = '{cheque.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe("ck_cheques_passed");
    }

    [Theory]
    [InlineData("amount = 0", "ck_cheques_amount_positive")]
    [InlineData("payee = '  '", "ck_cheques_payee_not_blank")]
    [InlineData("description = ''", "ck_cheques_description_not_blank")]
    public async Task Cheque_InvalidField_RejectedByACheckConstraint(string set, string constraint)
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE cheques SET {set} WHERE id = '{cheque.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe(constraint);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Owner, string Staff, Guid OwnerId)> ClientsAsync()
    {
        var ownerUser = await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client,
            await client.LoginForAccessTokenAsync("owner", TestUsers.Password),
            await client.LoginForAccessTokenAsync("staff", TestUsers.Password),
            ownerUser.Id);
    }

    /// <summary>The gym's today as the API sees it, so a date test never straddles midnight in Tehran.</summary>
    private async Task<DateOnly> TodayAsync()
    {
        await using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IGymCalendar>().Today();
    }

    private static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client, string token, decimal amount, DateOnly dueDate, string payee, string description) =>
            SendAsync(client, token, HttpMethod.Post, ChequesPath, new { amount, dueDate, payee, description });

    private static async Task<ChequeResponse> RegisterChequeAsync(
        HttpClient client, string token, decimal amount, DateOnly dueDate, string payee = "فروشگاه تجهیزات")
    {
        using var response = await RegisterAsync(client, token, amount, dueDate, payee, "قسط");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ChequeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> UpdateAsync(
        HttpClient client, string token, Guid id, decimal amount, DateOnly dueDate, string payee, string description,
        uint version) =>
            SendAsync(client, token, HttpMethod.Put, $"{ChequesPath}/{id}",
                new { amount, dueDate, payee, description, version });

    private static Task<HttpResponseMessage> PassAsync(HttpClient client, string token, Guid id) =>
        SendAsync(client, token, HttpMethod.Post, $"{ChequesPath}/{id}/pass", body: null);

    private static async Task<ChequeResponse> PassChequeAsync(HttpClient client, string token, Guid id)
    {
        using var response = await PassAsync(client, token, id);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ChequeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> CancelAsync(HttpClient client, string token, Guid id, string reason) =>
        SendAsync(client, token, HttpMethod.Post, $"{ChequesPath}/{id}/cancel", new { reason });

    private static async Task<ChequeResponse> CancelChequeAsync(
        HttpClient client, string token, Guid id, string reason = "ثبت اشتباه")
    {
        using var response = await CancelAsync(client, token, id, reason);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ChequeResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<ChequeListResponse> ListAsync(HttpClient client, string token, string query = "")
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"{ChequesPath}?{query}", body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ChequeListResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client, string token, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private static async Task<string?> FieldErrorCodeAsync(HttpResponseMessage response, string field)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString();
    }

    private static JsonElement Value(string? json, string property)
    {
        using var document = JsonDocument.Parse(json.ShouldNotBeNull());

        return document.RootElement.GetProperty(property).Clone();
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Cheque> StoredAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Cheques
            .AsNoTracking()
            .SingleAsync(cheque => cheque.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountChequesAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Cheques
            .CountAsync(TestContext.Current.CancellationToken);
    }
}
