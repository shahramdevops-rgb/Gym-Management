using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Expenses;
using Gym.Application.Payables;
using Gym.Application.Payables.ListPayables;
using Gym.Application.Payables.ListPayablesDueSoon;
using Gym.Application.Reports.GetNeedsAttention;
using Gym.Domain.Audit;
using Gym.Domain.Expenses;
using Gym.Domain.Payables;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Persistence.Seed;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Payables;

/// <summary>
/// <c>/api/payables</c> and the dashboard's «چک و قسط نزدیک سررسید». BUSINESS_RULES.md §9
/// <i>Cheques and instalments</i>: registered, edited while pending, paid (a cheque on or after its
/// date, an instalment any time) which records the expense, sent back to pending with a reason which
/// voids it, or cancelled with a reason; never deleted. §1: Owner only, reading included.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class PayableEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string PayablesPath = "/api/payables";
    private const string ExpensesPath = "/api/expenses";
    private const string NeedsAttentionPath = "/api/reports/needs-attention";
    private const string DueSoonPath = "/api/payables/due-soon";

    private static readonly Guid Equipment = ExpenseCategorySeed.All.Single(seed => seed.Name == "تجهیزات").Id;
    private static readonly Guid Rent = ExpenseCategorySeed.All.Single(seed => seed.Name == "اجاره").Id;

    // ---- Register ----

    [Fact]
    public async Task RegisterPayable_Cheque_Returns201WithEveryField()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var dueDate = (await TodayAsync()).AddDays(20);

        using var response = await RegisterAsync(client, owner, new
        {
            kind = "Cheque",
            amount = 50_000_000m,
            dueDate,
            payee = "  فروشگاه تجهیزات ",
            description = " تردمیل ",
            categoryId = Equipment,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var cheque = (await response.Content.ReadFromJsonAsync<PayableResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{PayablesPath}/{cheque.Id}");
        cheque.Kind.ShouldBe(PayableKind.Cheque);
        cheque.Amount.ShouldBe(50_000_000m);
        cheque.DueDate.ShouldBe(dueDate);
        cheque.Payee.ShouldBe("فروشگاه تجهیزات");
        cheque.Description.ShouldBe("تردمیل");
        cheque.CategoryId.ShouldBe(Equipment);
        cheque.CategoryName.ShouldBe("تجهیزات");
        cheque.InstallmentNumber.ShouldBeNull();
        cheque.InstallmentCount.ShouldBeNull();
        cheque.Status.ShouldBe(PayableStatus.Pending);
        cheque.RegisteredByUserId.ShouldBe(ownerId);
        cheque.PaidAt.ShouldBeNull();
        cheque.ExpenseId.ShouldBeNull();
        cheque.CancelledAt.ShouldBeNull();
    }

    [Fact]
    public async Task RegisterPayable_Installment_Returns201WithItsNumbers()
    {
        var (client, owner, _, _) = await ClientsAsync();

        var installment = await RegisterInstallmentAsync(client, owner, 5_000_000m, await TodayAsync(), number: 3, count: 12);

        installment.Kind.ShouldBe(PayableKind.Installment);
        installment.InstallmentNumber.ShouldBe(3);
        installment.InstallmentCount.ShouldBe(12);
    }

    [Fact]
    public async Task RegisterPayable_KindAndStatusAreSentAsTheirNames()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RegisterAsync(client, owner, ChequeBody(1_000m, await TodayAsync()));

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("kind").GetString().ShouldBe("Cheque");
        body.RootElement.GetProperty("status").GetString().ShouldBe("Pending");
    }

    [Fact]
    public async Task RegisterPayable_AsStaff_Returns403AndRegistersNothing()
    {
        var (client, _, staff, _) = await ClientsAsync();

        using var response = await RegisterAsync(client, staff, ChequeBody(1_000m, await TodayAsync()));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CountPayablesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RegisterPayable_DatedLastYear_Returns201()
    {
        // BUSINESS_RULES.md §9: one written long ago can still be entered late.
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RegisterAsync(client, owner, ChequeBody(1_000m, (await TodayAsync()).AddYears(-1)));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData("0", "الف", "ب", "amount", "Payables.AmountNotPositive")]
    [InlineData("1000.001", "الف", "ب", "amount", "Payables.AmountTooManyDecimals")]
    [InlineData("1000", "  ", "ب", "payee", "Payables.PayeeRequired")]
    [InlineData("1000", "الف", "  ", "description", "Payables.DescriptionRequired")]
    public async Task RegisterPayable_InvalidField_Returns400WithFieldCode(
        string amount, string payee, string description, string field, string code)
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RegisterAsync(client, owner, new
        {
            kind = "Cheque",
            amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
            dueDate = await TodayAsync(),
            payee,
            description,
            categoryId = Equipment,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, field)).ShouldBe(code);
    }

    [Fact]
    public async Task RegisterPayable_PayeeOverTheLimit_Returns400()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RegisterAsync(client, owner, new
        {
            kind = "Cheque",
            amount = 1_000m,
            dueDate = await TodayAsync(),
            payee = new string('ب', Payable.PayeeMaxLength + 1),
            description = "ب",
            categoryId = Equipment,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "payee")).ShouldBe("Payables.PayeeTooLong");
    }

    [Fact]
    public async Task RegisterPayable_NoCategory_Returns400()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RegisterAsync(client, owner, new
        {
            kind = "Cheque",
            amount = 1_000m,
            dueDate = await TodayAsync(),
            payee = "الف",
            description = "ب",
            categoryId = Guid.Empty,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "categoryId")).ShouldBe("Payables.CategoryRequired");
    }

    [Fact]
    public async Task RegisterPayable_UnknownCategory_Returns404()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RegisterAsync(client, owner, new
        {
            kind = "Cheque",
            amount = 1_000m,
            dueDate = await TodayAsync(),
            payee = "الف",
            description = "ب",
            categoryId = Guid.CreateVersion7(),
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payables.CategoryNotFound");
        (await CountPayablesAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData("Installment", null, null, "installmentNumber", "Payables.InstallmentNumbersRequired")]
    [InlineData("Installment", 13, 12, "installmentNumber", "Payables.InstallmentNumberOutOfRange")]
    [InlineData("Installment", 1, 361, "installmentCount", "Payables.InstallmentCountOutOfRange")]
    [InlineData("Cheque", 1, 12, "installmentNumber", "Payables.InstallmentNumbersOnlyForInstallments")]
    public async Task RegisterPayable_WrongInstallmentNumbers_Returns400WithFieldCode(
        string kind, int? installmentNumber, int? installmentCount, string field, string code)
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RegisterAsync(client, owner, new
        {
            kind,
            amount = 1_000m,
            dueDate = await TodayAsync(),
            payee = "الف",
            description = "ب",
            categoryId = Equipment,
            installmentNumber,
            installmentCount,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, field)).ShouldBe(code);
    }

    [Fact]
    public async Task RegisterPayable_UnknownKind_Returns400()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RegisterAsync(client, owner, new
        {
            kind = "Loan",
            amount = 1_000m,
            dueDate = await TodayAsync(),
            payee = "الف",
            description = "ب",
            categoryId = Equipment,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CountPayablesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RegisterPayable_AmountWithTwoDecimals_IsStoredExactly()
    {
        var (client, owner, _, _) = await ClientsAsync();

        var cheque = await RegisterChequeAsync(client, owner, 1_234_567.89m, await TodayAsync());

        (await StoredAsync(cheque.Id)).Amount.ShouldBe(1_234_567.89m);
    }

    // ---- Get ----

    [Fact]
    public async Task GetPayable_AsOwner_ReturnsIt()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{PayablesPath}/{cheque.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<PayableResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().Id.ShouldBe(cheque.Id);
    }

    [Fact]
    public async Task GetPayable_AsStaff_Returns403()
    {
        var (client, owner, staff, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await SendAsync(client, staff, HttpMethod.Get, $"{PayablesPath}/{cheque.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPayable_UnknownId_Returns404()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{PayablesPath}/{Guid.CreateVersion7()}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payables.NotFound");
    }

    // ---- Update ----

    [Fact]
    public async Task UpdatePayable_AsOwner_ReplacesTheFieldsTheKindIncluded()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);

        using var response = await UpdateAsync(client, owner, cheque.Id, new
        {
            kind = "Installment",
            amount = 2_500m,
            dueDate = today.AddDays(30),
            payee = "بانک ملت",
            description = "وام دستگاه",
            categoryId = Rent,
            installmentNumber = 2,
            installmentCount = 6,
            version = cheque.Version,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await StoredAsync(cheque.Id);
        stored.Kind.ShouldBe(PayableKind.Installment);
        stored.Amount.ShouldBe(2_500m);
        stored.DueDate.ShouldBe(today.AddDays(30));
        stored.Payee.ShouldBe("بانک ملت");
        stored.Description.ShouldBe("وام دستگاه");
        stored.CategoryId.ShouldBe(Rent);
        stored.InstallmentNumber.ShouldBe(2);
        stored.InstallmentCount.ShouldBe(6);
    }

    [Fact]
    public async Task UpdatePayable_AsOwner_WritesTheOldAndNewAmountToTheAuditLog()
    {
        // BUSINESS_RULES.md §9: every edit is audited.
        var (client, owner, _, ownerId) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);

        (await UpdateAsync(client, owner, cheque.Id, ChequeUpdateBody(2_500m, today, cheque.Version)))
            .EnsureSuccessStatusCode().Dispose();

        await using var scope = Fixture.CreateScope();
        var update = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .SingleAsync(
                log => log.EntityType == nameof(Payable) && log.EntityId == cheque.Id.ToString() && log.Action == AuditAction.Update,
                TestContext.Current.CancellationToken);
        update.UserId.ShouldBe(ownerId);
        Value(update.OldValues, nameof(Payable.Amount)).GetDecimal().ShouldBe(1_000m);
        Value(update.NewValues, nameof(Payable.Amount)).GetDecimal().ShouldBe(2_500m);
    }

    [Fact]
    public async Task UpdatePayable_AsStaff_Returns403()
    {
        var (client, owner, staff, _) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);

        using var response = await UpdateAsync(client, staff, cheque.Id, ChequeUpdateBody(2_500m, today, cheque.Version));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await StoredAsync(cheque.Id)).Amount.ShouldBe(1_000m);
    }

    [Fact]
    public async Task UpdatePayable_StaleVersion_Returns409AndKeepsTheNewerEdit()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);
        (await UpdateAsync(client, owner, cheque.Id, ChequeUpdateBody(2_000m, today, cheque.Version))).Dispose();

        using var response = await UpdateAsync(client, owner, cheque.Id, ChequeUpdateBody(3_000m, today, cheque.Version));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payables.ChangedConcurrently");
        (await StoredAsync(cheque.Id)).Amount.ShouldBe(2_000m);
    }

    [Fact]
    public async Task UpdatePayable_Paid_Returns422AndChangesNothing()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);
        var paid = await PayPayableAsync(client, owner, cheque.Id);

        using var response = await UpdateAsync(client, owner, cheque.Id, ChequeUpdateBody(2_000m, today, paid.Version));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payables.AlreadyPaid");
        (await StoredAsync(cheque.Id)).Amount.ShouldBe(1_000m);
    }

    [Fact]
    public async Task UpdatePayable_Cancelled_Returns422AndChangesNothing()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);
        var cancelled = await CancelPayableAsync(client, owner, cheque.Id);

        using var response = await UpdateAsync(client, owner, cheque.Id, ChequeUpdateBody(2_000m, today, cancelled.Version));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payables.AlreadyCancelled");
    }

    // ---- Pay ----

    [Fact]
    public async Task PayPayable_ChequeOnItsDate_MarksItPaidAndRecordsTheExpenseDatedToday()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 45_000_000m, today);

        using var response = await PayAsync(client, owner, cheque.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var paid = (await response.Content.ReadFromJsonAsync<PayableResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        paid.Status.ShouldBe(PayableStatus.Paid);
        paid.PaidAt.ShouldNotBeNull();
        paid.PaidByUserId.ShouldBe(ownerId);

        var expense = await StoredExpenseAsync(paid.ExpenseId.ShouldNotBeNull());
        expense.PayableId.ShouldBe(cheque.Id);
        expense.Amount.ShouldBe(45_000_000m);
        expense.CategoryId.ShouldBe(Equipment);
        expense.ExpenseDate.ShouldBe(today);
        expense.Description.ShouldBe("تردمیل");
        expense.RecordedByUserId.ShouldBe(ownerId);
        expense.IsVoided.ShouldBeFalse();
    }

    [Fact]
    public async Task PayPayable_ChequePastItsDate_DatesTheExpenseTodayNotOnTheCheque()
    {
        // BUSINESS_RULES.md §9: the expense is dated the day it was marked, when the money left.
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today.AddDays(-12));

        var paid = await PayPayableAsync(client, owner, cheque.Id);

        (await StoredExpenseAsync(paid.ExpenseId.ShouldNotBeNull())).ExpenseDate.ShouldBe(today);
    }

    [Fact]
    public async Task PayPayable_ChequeTomorrow_Returns422ChequeNotDueYetAndRecordsNothing()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, (await TodayAsync()).AddDays(1));

        using var response = await PayAsync(client, owner, cheque.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payables.ChequeNotDueYet");
        (await StoredAsync(cheque.Id)).IsPending.ShouldBeTrue();
        (await CountExpensesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task PayPayable_InstallmentMonthsAhead_Succeeds()
    {
        // BUSINESS_RULES.md §9: an instalment can be paid early.
        var (client, owner, _, _) = await ClientsAsync();
        var installment = await RegisterInstallmentAsync(client, owner, 5_000_000m, (await TodayAsync()).AddMonths(2), 1, 12);

        var paid = await PayPayableAsync(client, owner, installment.Id);

        paid.Status.ShouldBe(PayableStatus.Paid);
        paid.ExpenseId.ShouldNotBeNull();
    }

    [Fact]
    public async Task PayPayable_Twice_Returns422AlreadyPaidAndKeepsOneExpense()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await PayPayableAsync(client, owner, cheque.Id);

        using var response = await PayAsync(client, owner, cheque.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payables.AlreadyPaid");
        (await CountExpensesAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task PayPayable_InParallel_RecordsExactlyOneExpense()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        var responses = await Task.WhenAll(
            PayAsync(client, owner, cheque.Id),
            PayAsync(client, owner, cheque.Id),
            PayAsync(client, owner, cheque.Id));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            (await CountExpensesAsync()).ShouldBe(1);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task PayPayable_AsStaff_Returns403()
    {
        var (client, owner, staff, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await PayAsync(client, staff, cheque.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await StoredAsync(cheque.Id)).IsPending.ShouldBeTrue();
    }

    [Fact]
    public async Task PayPayable_UnknownId_Returns404()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await PayAsync(client, owner, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PayAndCancel_InParallel_ExactlyOneSucceeds()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        var responses = await Task.WhenAll(
            PayAsync(client, owner, cheque.Id),
            CancelAsync(client, owner, cheque.Id, "پس گرفته شد"),
            PayAsync(client, owner, cheque.Id),
            CancelAsync(client, owner, cheque.Id, "اشتباه"));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            responses.ShouldAllBe(response =>
                response.StatusCode == HttpStatusCode.OK ||
                response.StatusCode == HttpStatusCode.Conflict ||
                response.StatusCode == HttpStatusCode.UnprocessableEntity);
            var stored = await StoredAsync(cheque.Id);
            (stored.IsPaid ^ stored.IsCancelled).ShouldBeTrue();
            (await CountExpensesAsync()).ShouldBe(stored.IsPaid ? 1 : 0);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    // ---- Revert ----

    [Fact]
    public async Task RevertPayable_Paid_IsPendingAgainAndVoidsTheExpenseWithTheReason()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        var paid = await PayPayableAsync(client, owner, cheque.Id);

        using var response = await RevertAsync(client, owner, cheque.Id, "  اشتباهی زده شد ");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reverted = (await response.Content.ReadFromJsonAsync<PayableResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        reverted.Status.ShouldBe(PayableStatus.Pending);
        reverted.PaidAt.ShouldBeNull();
        reverted.ExpenseId.ShouldBeNull();

        var expense = await StoredExpenseAsync(paid.ExpenseId.ShouldNotBeNull());
        expense.IsVoided.ShouldBeTrue();
        expense.VoidReason.ShouldBe("اشتباهی زده شد");
        expense.VoidedByUserId.ShouldBe(ownerId);
        (await CountExpensesAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task RevertPayable_ThenPaidAgain_LeavesOneVoidedAndOneStandingExpense()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        var first = await PayPayableAsync(client, owner, cheque.Id);
        (await RevertAsync(client, owner, cheque.Id, "اشتباه")).EnsureSuccessStatusCode().Dispose();

        var second = await PayPayableAsync(client, owner, cheque.Id);

        second.ExpenseId.ShouldNotBeNull().ShouldNotBe(first.ExpenseId.ShouldNotBeNull());
        (await StoredExpenseAsync(first.ExpenseId.Value)).IsVoided.ShouldBeTrue();
        (await StoredExpenseAsync(second.ExpenseId.Value)).IsVoided.ShouldBeFalse();
    }

    [Fact]
    public async Task RevertPayable_Pending_Returns422NotPaid()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await RevertAsync(client, owner, cheque.Id, "اشتباه");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payables.NotPaid");
    }

    [Fact]
    public async Task RevertPayable_Cancelled_Returns422AlreadyCancelled()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await CancelPayableAsync(client, owner, cheque.Id);

        using var response = await RevertAsync(client, owner, cheque.Id, "اشتباه");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payables.AlreadyCancelled");
    }

    [Fact]
    public async Task RevertPayable_BlankReason_Returns400AndStaysPaid()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await PayPayableAsync(client, owner, cheque.Id);

        using var response = await RevertAsync(client, owner, cheque.Id, "   ");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "reason")).ShouldBe("Payables.RevertReasonRequired");
        (await StoredAsync(cheque.Id)).IsPaid.ShouldBeTrue();
    }

    [Fact]
    public async Task RevertPayable_AsStaff_Returns403()
    {
        var (client, owner, staff, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await PayPayableAsync(client, owner, cheque.Id);

        using var response = await RevertAsync(client, staff, cheque.Id, "اشتباه");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await StoredAsync(cheque.Id)).IsPaid.ShouldBeTrue();
    }

    // ---- The expense it recorded, seen from the expenses ----

    [Fact]
    public async Task ExpenseOfAPayment_ListedWithItsPayable()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        var paid = await PayPayableAsync(client, owner, cheque.Id);

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{ExpensesPath}/{paid.ExpenseId}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ExpenseResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().PayableId.ShouldBe(cheque.Id);
    }

    [Fact]
    public async Task ExpenseOfAPayment_EditedOnTheExpensesPage_Returns422LinkedToPayable()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, today);
        var paid = await PayPayableAsync(client, owner, cheque.Id);
        var expense = await StoredExpenseAsync(paid.ExpenseId.ShouldNotBeNull());

        using var response = await SendAsync(client, owner, HttpMethod.Put, $"{ExpensesPath}/{expense.Id}", new
        {
            amount = 2_000m,
            categoryId = Equipment,
            expenseDate = today,
            description = "دیگر",
            referenceNumber = (string?)null,
            version = expense.Version,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Expenses.LinkedToPayable");
        (await StoredExpenseAsync(expense.Id)).Amount.ShouldBe(1_000m);
    }

    [Fact]
    public async Task ExpenseOfAPayment_VoidedOnTheExpensesPage_Returns422LinkedToPayable()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        var paid = await PayPayableAsync(client, owner, cheque.Id);

        using var response = await SendAsync(
            client, owner, HttpMethod.Post, $"{ExpensesPath}/{paid.ExpenseId}/void", new { reason = "اشتباه" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Expenses.LinkedToPayable");
        (await StoredExpenseAsync(paid.ExpenseId.ShouldNotBeNull())).IsVoided.ShouldBeFalse();
    }

    // ---- Cancel ----

    [Fact]
    public async Task CancelPayable_AsOwner_KeepsTheRowMarkedWithTheReason()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, (await TodayAsync()).AddDays(10));

        var cancelled = await CancelPayableAsync(client, owner, cheque.Id, "  از فروشنده پس گرفته شد ");

        cancelled.Status.ShouldBe(PayableStatus.Cancelled);
        cancelled.CancelReason.ShouldBe("از فروشنده پس گرفته شد");
        cancelled.CancelledByUserId.ShouldBe(ownerId);
        (await CountPayablesAsync()).ShouldBe(1);
        (await CountExpensesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CancelPayable_Twice_Returns422AndKeepsTheFirstReason()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await CancelPayableAsync(client, owner, cheque.Id, "اول");

        using var response = await CancelAsync(client, owner, cheque.Id, "دوم");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payables.AlreadyCancelled");
        (await StoredAsync(cheque.Id)).CancelReason.ShouldBe("اول");
    }

    [Fact]
    public async Task CancelPayable_Paid_Returns422AlreadyPaid()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await PayPayableAsync(client, owner, cheque.Id);

        using var response = await CancelAsync(client, owner, cheque.Id, "دیر شد");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Payables.AlreadyPaid");
    }

    [Fact]
    public async Task CancelPayable_BlankReason_Returns400()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await CancelAsync(client, owner, cheque.Id, "   ");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "reason")).ShouldBe("Payables.CancelReasonRequired");
    }

    [Fact]
    public async Task CancelPayable_AsStaff_Returns403()
    {
        var (client, owner, staff, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await CancelAsync(client, staff, cheque.Id, "اشتباه");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await StoredAsync(cheque.Id)).IsPending.ShouldBeTrue();
    }

    [Fact]
    public async Task DeletePayable_AsOwner_HasNoEndpoint()
    {
        // BUSINESS_RULES.md §9: cancelled with a reason, never deleted.
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await SendAsync(client, owner, HttpMethod.Delete, $"{PayablesPath}/{cheque.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await CountPayablesAsync()).ShouldBe(1);
    }

    // ---- List ----

    [Fact]
    public async Task ListPayables_Pending_EarliestDateFirstWithThePendingTotals()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var later = await RegisterChequeAsync(client, owner, 3_000m, today.AddDays(40));
        var overdue = await RegisterChequeAsync(client, owner, 2_000m, today.AddDays(-3));
        var installment = await RegisterInstallmentAsync(client, owner, 700m, today.AddDays(10), 1, 4);
        var paid = await RegisterChequeAsync(client, owner, 10_000m, today.AddDays(-1));
        await PayPayableAsync(client, owner, paid.Id);
        var cancelled = await RegisterChequeAsync(client, owner, 20_000m, today.AddDays(5));
        await CancelPayableAsync(client, owner, cancelled.Id);

        var list = await ListAsync(client, owner, "status=Pending");

        list.Items.Select(payable => payable.Id).ShouldBe([overdue.Id, installment.Id, later.Id]);
        list.TotalCount.ShouldBe(3);
        list.PendingTotal.ShouldBe(5_700m);
        list.PendingChequeTotal.ShouldBe(5_000m);
        list.PendingInstallmentTotal.ShouldBe(700m);
    }

    [Theory]
    [InlineData("status=Paid", PayableStatus.Paid)]
    [InlineData("status=Cancelled", PayableStatus.Cancelled)]
    public async Task ListPayables_ByStatus_ListsOnlyThatStatusAndKeepsThePendingTotal(string query, PayableStatus status)
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        await RegisterChequeAsync(client, owner, 3_000m, today.AddDays(40));
        var paid = await RegisterChequeAsync(client, owner, 10_000m, today);
        await PayPayableAsync(client, owner, paid.Id);
        var cancelled = await RegisterChequeAsync(client, owner, 20_000m, today);
        await CancelPayableAsync(client, owner, cancelled.Id);

        var list = await ListAsync(client, owner, query);

        list.Items.ShouldHaveSingleItem().Status.ShouldBe(status);
        list.PendingTotal.ShouldBe(3_000m);
    }

    [Fact]
    public async Task ListPayables_ByKind_ListsOnlyThatKindAndKeepsEveryTotal()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        await RegisterChequeAsync(client, owner, 3_000m, today);
        var installment = await RegisterInstallmentAsync(client, owner, 700m, today, 2, 4);

        var list = await ListAsync(client, owner, "kind=Installment");

        list.Items.ShouldHaveSingleItem().Id.ShouldBe(installment.Id);
        list.PendingTotal.ShouldBe(3_700m);
        list.PendingChequeTotal.ShouldBe(3_000m);
        list.PendingInstallmentTotal.ShouldBe(700m);
    }

    [Fact]
    public async Task ListPayables_Paid_ShowsTheStandingExpenseAndTheCategoryName()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        var paid = await PayPayableAsync(client, owner, cheque.Id);

        var list = await ListAsync(client, owner, "status=Paid");

        var row = list.Items.ShouldHaveSingleItem();
        row.ExpenseId.ShouldBe(paid.ExpenseId);
        row.CategoryName.ShouldBe("تجهیزات");
    }

    [Fact]
    public async Task ListPayables_NoStatus_ListsEveryOneLatestDateFirst()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var early = await RegisterChequeAsync(client, owner, 1_000m, today.AddDays(-10));
        var late = await RegisterChequeAsync(client, owner, 1_000m, today.AddDays(10));
        var middle = await RegisterChequeAsync(client, owner, 1_000m, today);
        await CancelPayableAsync(client, owner, middle.Id);

        var list = await ListAsync(client, owner);

        list.Items.Select(payable => payable.Id).ShouldBe([late.Id, middle.Id, early.Id]);
    }

    [Fact]
    public async Task ListPayables_PendingTotalCoversEveryPageNotJustTheFirst()
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

    [Theory]
    [InlineData("status=Bounced")]
    [InlineData("kind=Loan")]
    public async Task ListPayables_UnknownFilter_Returns400(string query)
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{PayablesPath}?{query}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ListPayables_AsStaff_Returns403()
    {
        var (client, _, staff, _) = await ClientsAsync();

        using var response = await SendAsync(client, staff, HttpMethod.Get, PayablesPath, body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- The dashboard's reminder ----

    [Fact]
    public async Task NeedsAttention_Payables_ListsPendingWithinSevenDaysAndPastTheirDate()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var overdue = await RegisterChequeAsync(client, owner, 1_000m, today.AddDays(-15), "تأخیری");
        var dueToday = await RegisterChequeAsync(client, owner, 2_000m, today, "امروز");
        var installment = await RegisterInstallmentAsync(client, owner, 2_500m, today.AddDays(3), 4, 12);
        var daySeven = await RegisterChequeAsync(client, owner, 3_000m, today.AddDays(7), "روز هفتم");
        await RegisterChequeAsync(client, owner, 4_000m, today.AddDays(8), "روز هشتم");
        var paid = await RegisterChequeAsync(client, owner, 5_000m, today.AddDays(-1), "پاس شده");
        await PayPayableAsync(client, owner, paid.Id);
        var cancelled = await RegisterChequeAsync(client, owner, 6_000m, today.AddDays(2), "باطل");
        await CancelPayableAsync(client, owner, cancelled.Id);

        using var response = await SendAsync(client, owner, HttpMethod.Get, NeedsAttentionPath, body: null);
        response.EnsureSuccessStatusCode();
        var needs = (await response.Content.ReadFromJsonAsync<NeedsAttentionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

        // Day 7 is in and day 8 is out; past its date stays until marked; the earliest first.
        needs.PayablesDue.ShouldBe(
        [
            new PayableDueResponse(overdue.Id, PayableKind.Cheque, "تأخیری", 1_000m, today.AddDays(-15), "تردمیل", null, null),
            new PayableDueResponse(dueToday.Id, PayableKind.Cheque, "امروز", 2_000m, today, "تردمیل", null, null),
            new PayableDueResponse(installment.Id, PayableKind.Installment, "بانک ملت", 2_500m, today.AddDays(3), "وام دستگاه", 4, 12),
            new PayableDueResponse(daySeven.Id, PayableKind.Cheque, "روز هفتم", 3_000m, today.AddDays(7), "تردمیل", null, null),
        ]);
    }

    // ---- The header's alert ----

    [Fact]
    public async Task ListPayablesDueSoon_ListsPendingWithinFiveDaysAndPastTheirDate()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var overdue = await RegisterChequeAsync(client, owner, 1_000m, today.AddDays(-2), "تأخیری");
        var dueToday = await RegisterChequeAsync(client, owner, 2_000m, today, "امروز");
        var installment = await RegisterInstallmentAsync(client, owner, 2_500m, today.AddDays(3), 4, 12);
        var dayFive = await RegisterChequeAsync(client, owner, 3_000m, today.AddDays(5), "روز پنجم");
        await RegisterChequeAsync(client, owner, 4_000m, today.AddDays(6), "روز ششم");
        var paid = await RegisterChequeAsync(client, owner, 5_000m, today.AddDays(-1), "پاس شده");
        await PayPayableAsync(client, owner, paid.Id);
        var cancelled = await RegisterChequeAsync(client, owner, 6_000m, today.AddDays(2), "باطل");
        await CancelPayableAsync(client, owner, cancelled.Id);

        using var response = await SendAsync(client, owner, HttpMethod.Get, DueSoonPath, body: null);
        response.EnsureSuccessStatusCode();
        var dueSoon = (await response.Content.ReadFromJsonAsync<PayablesDueSoonResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

        // Day 5 is in and day 6 is out; past its date stays until marked; the earliest first.
        dueSoon.Today.ShouldBe(today);
        dueSoon.Items.ShouldBe(
        [
            new PayableDueSoonResponse(overdue.Id, PayableKind.Cheque, "تأخیری", 1_000m, today.AddDays(-2), null, null, -2),
            new PayableDueSoonResponse(dueToday.Id, PayableKind.Cheque, "امروز", 2_000m, today, null, null, 0),
            new PayableDueSoonResponse(installment.Id, PayableKind.Installment, "بانک ملت", 2_500m, today.AddDays(3), 4, 12, 3),
            new PayableDueSoonResponse(dayFive.Id, PayableKind.Cheque, "روز پنجم", 3_000m, today.AddDays(5), null, null, 5),
        ]);
    }

    [Fact]
    public async Task ListPayablesDueSoon_NothingClose_ReturnsAnEmptyList()
    {
        var (client, owner, _, _) = await ClientsAsync();
        await RegisterChequeAsync(client, owner, 1_000m, (await TodayAsync()).AddDays(6));

        using var response = await SendAsync(client, owner, HttpMethod.Get, DueSoonPath, body: null);
        response.EnsureSuccessStatusCode();
        var dueSoon = (await response.Content.ReadFromJsonAsync<PayablesDueSoonResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

        dueSoon.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListPayablesDueSoon_KindIsSentAsItsName()
    {
        var (client, owner, _, _) = await ClientsAsync();
        await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        using var response = await SendAsync(client, owner, HttpMethod.Get, DueSoonPath, body: null);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("items")[0].GetProperty("kind").GetString().ShouldBe("Cheque");
    }

    [Fact]
    public async Task ListPayablesDueSoon_AsStaff_Returns403()
    {
        var (client, _, staff, _) = await ClientsAsync();

        using var response = await SendAsync(client, staff, HttpMethod.Get, DueSoonPath, body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- The database's own copy of the rules ----

    [Fact]
    public async Task Payable_PaidAndCancelled_RejectedByACheckConstraint()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await PayPayableAsync(client, owner, cheque.Id);

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE payables SET cancelled_at = now(), cancel_reason = 'x', cancelled_by_user_id = '{ownerId}' WHERE id = '{cheque.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe("ck_payables_not_paid_and_cancelled");
    }

    [Fact]
    public async Task Payable_CancelledWithoutAReason_RejectedByACheckConstraint()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE payables SET cancelled_at = now(), cancelled_by_user_id = '{ownerId}' WHERE id = '{cheque.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe("ck_payables_cancelled");
    }

    [Fact]
    public async Task Payable_PaidWithoutAUser_RejectedByACheckConstraint()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE payables SET paid_at = now() WHERE id = '{cheque.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe("ck_payables_paid");
    }

    [Theory]
    [InlineData("amount = 0", "ck_payables_amount_positive")]
    [InlineData("payee = '  '", "ck_payables_payee_not_blank")]
    [InlineData("description = ''", "ck_payables_description_not_blank")]
    [InlineData("kind = 'Loan'", "ck_payables_kind")]
    [InlineData("installment_number = 1, installment_count = 12", "ck_payables_installment_numbers")]
    [InlineData("kind = 'Installment'", "ck_payables_installment_numbers")]
    [InlineData("kind = 'Installment', installment_number = 5, installment_count = 4", "ck_payables_installment_numbers")]
    [InlineData("kind = 'Installment', installment_number = 1, installment_count = 361", "ck_payables_installment_numbers")]
    public async Task Payable_InvalidField_RejectedByACheckConstraint(string set, string constraint)
    {
        var (client, owner, _, _) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            $"UPDATE payables SET {set} WHERE id = '{cheque.Id}'"));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe(constraint);
    }

    [Fact]
    public async Task Payable_SecondStandingExpense_RejectedByThePartialUniqueIndex()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await PayPayableAsync(client, owner, cheque.Id);

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(
            "INSERT INTO expenses (id, amount, category_id, expense_date, description, recorded_by_user_id, payable_id, created_at) " +
            $"VALUES ('{Guid.CreateVersion7()}', 1000, '{Equipment}', current_date, 'x', '{ownerId}', '{cheque.Id}', now())"));

        exception.SqlState.ShouldBe("23505");
        exception.ConstraintName.ShouldBe(PayableConstraints.OneStandingExpense);
    }

    [Fact]
    public async Task Payable_SecondVoidedExpense_IsAllowedByThePartialUniqueIndex()
    {
        // A payment sent back to pending leaves a voided expense; only standing ones are unique.
        var (client, owner, _, ownerId) = await ClientsAsync();
        var cheque = await RegisterChequeAsync(client, owner, 1_000m, await TodayAsync());
        await PayPayableAsync(client, owner, cheque.Id);

        await ExecuteSqlAsync(
            "INSERT INTO expenses (id, amount, category_id, expense_date, description, recorded_by_user_id, payable_id, " +
            "voided_at, void_reason, voided_by_user_id, created_at) " +
            $"VALUES ('{Guid.CreateVersion7()}', 1000, '{Equipment}', current_date, 'x', '{ownerId}', '{cheque.Id}', " +
            $"now(), 'اشتباه', '{ownerId}', now())");

        (await CountExpensesAsync()).ShouldBe(2);
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

    private static object ChequeBody(decimal amount, DateOnly dueDate, string payee = "فروشگاه تجهیزات") =>
        new { kind = "Cheque", amount, dueDate, payee, description = "تردمیل", categoryId = Equipment };

    private static object ChequeUpdateBody(decimal amount, DateOnly dueDate, uint version) =>
        new { kind = "Cheque", amount, dueDate, payee = "الف", description = "ب", categoryId = Equipment, version };

    private static Task<HttpResponseMessage> RegisterAsync(HttpClient client, string token, object body) =>
        SendAsync(client, token, HttpMethod.Post, PayablesPath, body);

    private static async Task<PayableResponse> RegisterChequeAsync(
        HttpClient client, string token, decimal amount, DateOnly dueDate, string payee = "فروشگاه تجهیزات")
    {
        using var response = await RegisterAsync(client, token, ChequeBody(amount, dueDate, payee));
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PayableResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<PayableResponse> RegisterInstallmentAsync(
        HttpClient client, string token, decimal amount, DateOnly dueDate, int number, int count)
    {
        using var response = await RegisterAsync(client, token, new
        {
            kind = "Installment",
            amount,
            dueDate,
            payee = "بانک ملت",
            description = "وام دستگاه",
            categoryId = Equipment,
            installmentNumber = number,
            installmentCount = count,
        });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PayableResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> UpdateAsync(HttpClient client, string token, Guid id, object body) =>
        SendAsync(client, token, HttpMethod.Put, $"{PayablesPath}/{id}", body);

    private static Task<HttpResponseMessage> PayAsync(HttpClient client, string token, Guid id) =>
        SendAsync(client, token, HttpMethod.Post, $"{PayablesPath}/{id}/pay", body: null);

    private static async Task<PayableResponse> PayPayableAsync(HttpClient client, string token, Guid id)
    {
        using var response = await PayAsync(client, token, id);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PayableResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> RevertAsync(HttpClient client, string token, Guid id, string reason) =>
        SendAsync(client, token, HttpMethod.Post, $"{PayablesPath}/{id}/revert", new { reason });

    private static Task<HttpResponseMessage> CancelAsync(HttpClient client, string token, Guid id, string reason) =>
        SendAsync(client, token, HttpMethod.Post, $"{PayablesPath}/{id}/cancel", new { reason });

    private static async Task<PayableResponse> CancelPayableAsync(
        HttpClient client, string token, Guid id, string reason = "ثبت اشتباه")
    {
        using var response = await CancelAsync(client, token, id, reason);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PayableResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<PayableListResponse> ListAsync(HttpClient client, string token, string query = "")
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"{PayablesPath}?{query}", body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PayableListResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
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

    private async Task<Payable> StoredAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payables
            .AsNoTracking()
            .SingleAsync(payable => payable.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<Expense> StoredExpenseAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Expenses
            .AsNoTracking()
            .SingleAsync(expense => expense.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountPayablesAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Payables
            .CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task<int> CountExpensesAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Expenses
            .CountAsync(TestContext.Current.CancellationToken);
    }
}
