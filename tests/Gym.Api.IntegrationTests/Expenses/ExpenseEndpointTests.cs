using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Expenses;
using Gym.Application.Expenses.ListExpenses;
using Gym.Domain.Audit;
using Gym.Domain.Expenses;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Persistence.Seed;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Expenses;

/// <summary>
/// <c>/api/expenses</c>. BUSINESS_RULES.md §9: an expense is recorded, edited while it stands,
/// and voided with a reason, never deleted; voided expenses count toward no total. §1: Owner only.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ExpenseEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string ExpensesPath = "/api/expenses";

    private static readonly Guid Rent = ExpenseCategorySeed.All.Single(seed => seed.Name == "اجاره").Id;
    private static readonly Guid Electricity = ExpenseCategorySeed.All.Single(seed => seed.Name == "برق").Id;

    // ---- Record ----

    [Fact]
    public async Task RecordExpense_AsOwner_Returns201WithEveryField()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var today = await TodayAsync();

        using var response = await RecordAsync(client, owner, 25_000_000m, Rent, today, "  اجاره مهر ", " 4411 ");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var expense = (await response.Content.ReadFromJsonAsync<ExpenseResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{ExpensesPath}/{expense.Id}");
        expense.Amount.ShouldBe(25_000_000m);
        expense.CategoryId.ShouldBe(Rent);
        expense.CategoryName.ShouldBe("اجاره");
        expense.ExpenseDate.ShouldBe(today);
        expense.Description.ShouldBe("اجاره مهر");
        expense.ReferenceNumber.ShouldBe("4411");
        expense.RecordedByUserId.ShouldBe(ownerId);
        expense.IsVoided.ShouldBeFalse();
    }

    [Fact]
    public async Task RecordExpense_AsStaff_Returns403AndRecordsNothing()
    {
        var (client, _, staff, _) = await ClientsAsync();

        using var response = await RecordAsync(client, staff, 1_000m, Rent, await TodayAsync(), "اجاره");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CountExpensesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RecordExpense_DatedTomorrow_Returns400DateInFuture()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RecordAsync(client, owner, 1_000m, Rent, (await TodayAsync()).AddDays(1), "اجاره");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe("Expenses.DateInFuture");
    }

    [Fact]
    public async Task RecordExpense_DatedLastYear_Returns201()
    {
        // BUSINESS_RULES.md §9: an old bill can still be entered.
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RecordAsync(client, owner, 1_000m, Rent, (await TodayAsync()).AddYears(-1), "اجاره");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task RecordExpense_UnknownCategory_Returns404()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await RecordAsync(client, owner, 1_000m, Guid.CreateVersion7(), await TodayAsync(), "اجاره");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Expenses.CategoryNotFound");
    }

    [Theory]
    [InlineData("0", "", "amount", "Expenses.AmountNotPositive")]
    [InlineData("1000.001", "اجاره", "amount", "Expenses.AmountTooManyDecimals")]
    [InlineData("1000", "   ", "description", "Expenses.DescriptionRequired")]
    public async Task RecordExpense_InvalidField_Returns400WithFieldCode(
        string amount, string description, string field, string code)
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Post, ExpensesPath, new
        {
            amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
            categoryId = Rent,
            expenseDate = await TodayAsync(),
            description,
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, field)).ShouldBe(code);
    }

    [Fact]
    public async Task RecordExpense_AmountWithTwoDecimals_IsStoredExactly()
    {
        var (client, owner, _, _) = await ClientsAsync();

        var expense = await RecordExpenseAsync(client, owner, 1_234_567.89m, Rent, await TodayAsync());

        (await StoredAsync(expense.Id)).Amount.ShouldBe(1_234_567.89m);
    }

    // ---- Get ----

    [Fact]
    public async Task GetExpense_AsOwner_ReturnsIt()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, await TodayAsync());

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{ExpensesPath}/{expense.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ExpenseResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().CategoryName.ShouldBe("اجاره");
    }

    [Fact]
    public async Task GetExpense_AsStaff_Returns403()
    {
        var (client, owner, staff, _) = await ClientsAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, await TodayAsync());

        using var response = await SendAsync(client, staff, HttpMethod.Get, $"{ExpensesPath}/{expense.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetExpense_UnknownId_Returns404()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{ExpensesPath}/{Guid.CreateVersion7()}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Expenses.NotFound");
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateExpense_AsOwner_ReplacesTheFields()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, today);

        using var response = await UpdateAsync(client, owner, expense.Id, 2_500m, Electricity, today.AddDays(-2), "قبض برق", expense.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<ExpenseResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        updated.Amount.ShouldBe(2_500m);
        updated.CategoryName.ShouldBe("برق");
        updated.ExpenseDate.ShouldBe(today.AddDays(-2));
        updated.Description.ShouldBe("قبض برق");
    }

    [Fact]
    public async Task UpdateExpense_AsOwner_WritesTheOldAndNewAmountToTheAuditLog()
    {
        // BUSINESS_RULES.md §9: every edit is audited, so an edit can replace the row.
        var (client, owner, _, ownerId) = await ClientsAsync();
        var today = await TodayAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, today);

        (await UpdateAsync(client, owner, expense.Id, 2_500m, Rent, today, "اجاره", expense.Version)).EnsureSuccessStatusCode().Dispose();

        await using var scope = Fixture.CreateScope();
        var update = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .SingleAsync(
                log => log.EntityType == nameof(Expense) && log.EntityId == expense.Id.ToString() && log.Action == AuditAction.Update,
                TestContext.Current.CancellationToken);
        update.UserId.ShouldBe(ownerId);
        Value(update.OldValues, nameof(Expense.Amount)).GetDecimal().ShouldBe(1_000m);
        Value(update.NewValues, nameof(Expense.Amount)).GetDecimal().ShouldBe(2_500m);
    }

    [Fact]
    public async Task UpdateExpense_AsStaff_Returns403()
    {
        var (client, owner, staff, _) = await ClientsAsync();
        var today = await TodayAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, today);

        using var response = await UpdateAsync(client, staff, expense.Id, 2_500m, Rent, today, "اجاره", expense.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await StoredAsync(expense.Id)).Amount.ShouldBe(1_000m);
    }

    [Fact]
    public async Task UpdateExpense_StaleVersion_Returns409AndKeepsTheNewerEdit()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, today);
        (await UpdateAsync(client, owner, expense.Id, 2_000m, Rent, today, "اجاره", expense.Version)).Dispose();

        using var response = await UpdateAsync(client, owner, expense.Id, 3_000m, Rent, today, "اجاره", expense.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Expenses.ChangedConcurrently");
        (await StoredAsync(expense.Id)).Amount.ShouldBe(2_000m);
    }

    [Fact]
    public async Task UpdateExpense_Voided_Returns422AndChangesNothing()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, today);
        var voided = await VoidExpenseAsync(client, owner, expense.Id);

        using var response = await UpdateAsync(client, owner, expense.Id, 2_000m, Rent, today, "اجاره", voided.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Expenses.AlreadyVoided");
        (await StoredAsync(expense.Id)).Amount.ShouldBe(1_000m);
    }

    [Fact]
    public async Task UpdateExpense_DatedTomorrow_Returns400()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, today);

        using var response = await UpdateAsync(client, owner, expense.Id, 1_000m, Rent, today.AddDays(1), "اجاره", expense.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe("Expenses.DateInFuture");
    }

    // ---- Void ----

    [Fact]
    public async Task VoidExpense_AsOwner_KeepsTheRowMarkedWithTheReason()
    {
        var (client, owner, _, ownerId) = await ClientsAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, await TodayAsync());

        var voided = await VoidExpenseAsync(client, owner, expense.Id, "  ثبت تکراری ");

        voided.IsVoided.ShouldBeTrue();
        voided.VoidReason.ShouldBe("ثبت تکراری");
        voided.VoidedByUserId.ShouldBe(ownerId);
        voided.VoidedAt.ShouldNotBeNull();
        (await CountExpensesAsync()).ShouldBe(1, "a voided expense is kept, never deleted.");
    }

    [Fact]
    public async Task VoidExpense_AsStaff_Returns403()
    {
        var (client, owner, staff, _) = await ClientsAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, await TodayAsync());

        using var response = await VoidAsync(client, staff, expense.Id, "ثبت تکراری");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await StoredAsync(expense.Id)).IsVoided.ShouldBeFalse();
    }

    [Fact]
    public async Task VoidExpense_Twice_Returns422AndKeepsTheFirstReason()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, await TodayAsync());
        await VoidExpenseAsync(client, owner, expense.Id, "ثبت تکراری");

        using var response = await VoidAsync(client, owner, expense.Id, "دلیل دیگر");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Expenses.AlreadyVoided");
        (await StoredAsync(expense.Id)).VoidReason.ShouldBe("ثبت تکراری");
    }

    [Fact]
    public async Task VoidExpense_BlankReason_Returns400()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, await TodayAsync());

        using var response = await VoidAsync(client, owner, expense.Id, "  ");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "reason")).ShouldBe("Expenses.VoidReasonRequired");
    }

    [Fact]
    public async Task VoidExpense_FourInParallel_ExactlyOneSucceeds()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, await TodayAsync());

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 4).Select(i => VoidAsync(client, owner, expense.Id, $"دلیل {i}")));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            responses.ShouldAllBe(response =>
                response.StatusCode == HttpStatusCode.OK ||
                response.StatusCode == HttpStatusCode.Conflict ||
                response.StatusCode == HttpStatusCode.UnprocessableEntity);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    // ---- List and totals ----

    [Fact]
    public async Task ListExpenses_VoidedExpense_IsLeftOutOfTheTotal()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        await RecordExpenseAsync(client, owner, 1_000m, Rent, today);
        await RecordExpenseAsync(client, owner, 250.50m, Electricity, today);
        var mistake = await RecordExpenseAsync(client, owner, 9_000m, Rent, today);
        await VoidExpenseAsync(client, owner, mistake.Id);

        var list = await ListAsync(client, owner);

        list.TotalAmount.ShouldBe(1_250.50m);
        list.TotalCount.ShouldBe(3, "voided expenses are listed by default, marked as voided.");
        list.Items.Single(item => item.Id == mistake.Id).IsVoided.ShouldBeTrue();
    }

    [Fact]
    public async Task ListExpenses_IncludeVoidedFalse_HidesVoidedAndKeepsTheSameTotal()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var kept = await RecordExpenseAsync(client, owner, 1_000m, Rent, today);
        var mistake = await RecordExpenseAsync(client, owner, 9_000m, Rent, today);
        await VoidExpenseAsync(client, owner, mistake.Id);

        var list = await ListAsync(client, owner, "includeVoided=false");

        list.Items.Select(item => item.Id).ShouldBe([kept.Id]);
        list.TotalCount.ShouldBe(1);
        list.TotalAmount.ShouldBe(1_000m);
    }

    [Fact]
    public async Task ListExpenses_EverythingVoided_TotalIsZero()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, await TodayAsync());
        await VoidExpenseAsync(client, owner, expense.Id);

        (await ListAsync(client, owner)).TotalAmount.ShouldBe(0m);
    }

    [Fact]
    public async Task ListExpenses_DateRange_IsInclusiveAtBothEnds()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        await RecordExpenseAsync(client, owner, 1m, Rent, today.AddDays(-10));
        var first = await RecordExpenseAsync(client, owner, 10m, Rent, today.AddDays(-7));
        var last = await RecordExpenseAsync(client, owner, 100m, Rent, today.AddDays(-3));
        await RecordExpenseAsync(client, owner, 1_000m, Rent, today);

        var list = await ListAsync(client, owner, $"from={today.AddDays(-7):yyyy-MM-dd}&to={today.AddDays(-3):yyyy-MM-dd}");

        list.Items.Select(item => item.Id).ShouldBe([last.Id, first.Id]);
        list.TotalAmount.ShouldBe(110m);
    }

    [Fact]
    public async Task ListExpenses_ByCategory_FiltersTheItemsAndTheTotal()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        await RecordExpenseAsync(client, owner, 1_000m, Rent, today);
        var bill = await RecordExpenseAsync(client, owner, 300m, Electricity, today);

        var list = await ListAsync(client, owner, $"categoryId={Electricity}");

        list.Items.Select(item => item.Id).ShouldBe([bill.Id]);
        list.TotalAmount.ShouldBe(300m);
    }

    [Fact]
    public async Task ListExpenses_TotalCoversEveryPageNotJustTheFirst()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        for (var i = 0; i < 3; i++)
        {
            await RecordExpenseAsync(client, owner, 100m, Rent, today);
        }

        var list = await ListAsync(client, owner, "pageSize=1");

        list.Items.Count.ShouldBe(1);
        list.TotalCount.ShouldBe(3);
        list.TotalAmount.ShouldBe(300m);
    }

    [Fact]
    public async Task ListExpenses_NewestDayFirst()
    {
        var (client, owner, _, _) = await ClientsAsync();
        var today = await TodayAsync();
        var older = await RecordExpenseAsync(client, owner, 1m, Rent, today.AddDays(-5));
        var newer = await RecordExpenseAsync(client, owner, 1m, Rent, today);
        var middle = await RecordExpenseAsync(client, owner, 1m, Rent, today.AddDays(-2));

        (await ListAsync(client, owner)).Items.Select(item => item.Id).ShouldBe([newer.Id, middle.Id, older.Id]);
    }

    [Fact]
    public async Task ListExpenses_BackwardsRange_Returns400()
    {
        var (client, owner, _, _) = await ClientsAsync();

        using var response = await SendAsync(
            client, owner, HttpMethod.Get, $"{ExpensesPath}?from=2026-09-10&to=2026-09-01", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldErrorCodeAsync(response, "to")).ShouldBe("Expenses.InvalidDateRange");
    }

    [Fact]
    public async Task ListExpenses_AsStaff_Returns403()
    {
        var (client, _, staff, _) = await ClientsAsync();

        using var response = await SendAsync(client, staff, HttpMethod.Get, ExpensesPath, body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteExpense_AsOwner_HasNoEndpoint()
    {
        // BUSINESS_RULES.md §9: voided with a reason, never deleted.
        var (client, owner, _, _) = await ClientsAsync();
        var expense = await RecordExpenseAsync(client, owner, 1_000m, Rent, await TodayAsync());

        using var response = await SendAsync(client, owner, HttpMethod.Delete, $"{ExpensesPath}/{expense.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await CountExpensesAsync()).ShouldBe(1);
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

    private static Task<HttpResponseMessage> RecordAsync(
        HttpClient client, string token, decimal amount, Guid categoryId, DateOnly expenseDate, string description,
        string? referenceNumber = null) =>
            SendAsync(client, token, HttpMethod.Post, ExpensesPath,
                new { amount, categoryId, expenseDate, description, referenceNumber });

    private static async Task<ExpenseResponse> RecordExpenseAsync(
        HttpClient client, string token, decimal amount, Guid categoryId, DateOnly expenseDate)
    {
        using var response = await RecordAsync(client, token, amount, categoryId, expenseDate, "هزینه");
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ExpenseResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> UpdateAsync(
        HttpClient client, string token, Guid id, decimal amount, Guid categoryId, DateOnly expenseDate,
        string description, uint version) =>
            SendAsync(client, token, HttpMethod.Put, $"{ExpensesPath}/{id}",
                new { amount, categoryId, expenseDate, description, referenceNumber = (string?)null, version });

    private static Task<HttpResponseMessage> VoidAsync(HttpClient client, string token, Guid id, string reason) =>
        SendAsync(client, token, HttpMethod.Post, $"{ExpensesPath}/{id}/void", new { reason });

    private static async Task<ExpenseResponse> VoidExpenseAsync(
        HttpClient client, string token, Guid id, string reason = "ثبت اشتباه")
    {
        using var response = await VoidAsync(client, token, id, reason);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ExpenseResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<ExpenseListResponse> ListAsync(HttpClient client, string token, string query = "")
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"{ExpensesPath}?{query}", body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ExpenseListResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
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

    private async Task<Expense> StoredAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Expenses
            .AsNoTracking()
            .SingleAsync(expense => expense.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountExpensesAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Expenses
            .CountAsync(TestContext.Current.CancellationToken);
    }
}
