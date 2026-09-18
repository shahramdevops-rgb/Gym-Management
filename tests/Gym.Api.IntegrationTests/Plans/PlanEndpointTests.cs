using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Paging;
using Gym.Application.Plans;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Plans;

/// <summary>
/// <c>/api/plans</c>. The rules under test are BUSINESS_RULES.md §3 and the permissions table in §1:
/// the Owner sets plans up, staff can only read them.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class PlanEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string PlansPath = "/api/plans";
    private const char ArabicYe = (char)0x064A;

    // ---- Create ----

    [Fact]
    public async Task CreatePlan_AsOwner_Returns201WithTheValues()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, "  یک ماهه ۱۲ جلسه ", 30, 12, 900_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var plan = (await response.Content.ReadFromJsonAsync<PlanResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{PlansPath}/{plan.Id}");
        plan.Name.ShouldBe("یک ماهه ۱۲ جلسه");
        plan.DurationDays.ShouldBe(30);
        plan.SessionCount.ShouldBe(12);
        plan.Price.ShouldBe(900_000m);
        plan.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task CreatePlan_NoSessionCount_IsStoredAsUnlimited()
    {
        var (client, owner, _) = await ClientsAsync();

        var plan = await CreatePlanAsync(client, owner, "ماهانه آزاد", 30, null, 1_500_000m);

        plan.SessionCount.ShouldBeNull();
        (await StoredAsync(plan.Id)).SessionCount.ShouldBeNull();
    }

    [Fact]
    public async Task CreatePlan_PriceWithCents_RoundTripsExactly()
    {
        var (client, owner, _) = await ClientsAsync();

        var plan = await CreatePlanAsync(client, owner, "پلن", 30, null, 1_234_567_890.99m);

        (await StoredAsync(plan.Id)).Price.ShouldBe(1_234_567_890.99m);
        (await GetAsync(client, owner, plan.Id)).Price.ShouldBe(1_234_567_890.99m);
    }

    /// <summary>
    /// The web form sends the price as a JSON string: the largest allowed price has more digits
    /// than a JavaScript number holds exactly, so it never passes through one.
    /// </summary>
    [Fact]
    public async Task CreatePlan_PriceAsJsonString_IsStoredExactly()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Post, PlansPath,
            new { name = "پلن", durationDays = 30, sessionCount = (int?)null, price = "9999999999999999.99" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var plan = (await response.Content.ReadFromJsonAsync<PlanResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        (await StoredAsync(plan.Id)).Price.ShouldBe(9_999_999_999_999_999.99m);
    }

    [Theory]
    [InlineData("   ", 30, null, "0", "name", "Plans.NameRequired")]
    [InlineData("پلن", 0, null, "0", "durationDays", "Plans.DurationInvalid")]
    [InlineData("پلن", 366, null, "0", "durationDays", "Plans.DurationInvalid")]
    [InlineData("پلن", 30, 0, "0", "sessionCount", "Plans.SessionCountInvalid")]
    [InlineData("پلن", 30, 366, "0", "sessionCount", "Plans.SessionCountInvalid")]
    [InlineData("پلن", 30, null, "-1", "price", "Plans.PriceNegative")]
    [InlineData("پلن", 30, null, "10.001", "price", "Plans.PriceTooManyDecimals")]
    public async Task CreatePlan_InvalidField_Returns400WithFieldCode(
        string name, int durationDays, int? sessionCount, string price, string field, string expectedCode)
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, name, durationDays, sessionCount, decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString().ShouldBe(expectedCode);
        (await CountPlansAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreatePlan_SameNameWithArabicYe_Returns409NameAlreadyExists()
    {
        var (client, owner, _) = await ClientsAsync();
        await CreatePlanAsync(client, owner, "ویژه", 30, null, 0m);

        using var response = await CreateAsync(client, owner, $" و{ArabicYe}ژه ", 60, 10, 5m);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Plans.NameAlreadyExists");
    }

    [Fact]
    public async Task CreatePlan_NameOfAnInactivePlan_IsStillADuplicate()
    {
        var (client, owner, _) = await ClientsAsync();
        var plan = await CreatePlanAsync(client, owner, "ویژه", 30, null, 0m);
        using var deactivated = await PostAsync(client, owner, $"{PlansPath}/{plan.Id}/deactivate");

        using var response = await CreateAsync(client, owner, "ویژه", 30, null, 0m);

        (await response.ReadErrorCodeAsync()).ShouldBe("Plans.NameAlreadyExists");
    }

    [Fact]
    public async Task CreatePlan_SameNameInParallel_OneWinsAndTheRestGet409()
    {
        var (client, owner, _) = await ClientsAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CreateAsync(client, owner, "ویژه", 30, null, 0m)));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(5);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    // ---- Permissions ----

    [Fact]
    public async Task CreatePlan_AsStaff_Returns403AndCreatesNothing()
    {
        var (client, _, staff) = await ClientsAsync();

        using var response = await CreateAsync(client, staff, "پلن", 30, null, 0m);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.Forbidden");
        (await CountPlansAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData("update")]
    [InlineData("activate")]
    [InlineData("deactivate")]
    public async Task ChangePlan_AsStaff_Returns403AndChangesNothing(string action)
    {
        var (client, owner, staff) = await ClientsAsync();
        var plan = await CreatePlanAsync(client, owner, "پلن", 30, null, 100m);

        using var response = action == "update"
            ? await UpdateAsync(client, staff, plan.Id, "نام دیگر", 30, null, 100m, plan.Version)
            : await PostAsync(client, staff, $"{PlansPath}/{plan.Id}/{action}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.Forbidden");
        var stored = await StoredAsync(plan.Id);
        stored.Name.ShouldBe("پلن");
        stored.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task ListAndGetPlans_AsStaff_Return200()
    {
        var (client, owner, staff) = await ClientsAsync();
        var plan = await CreatePlanAsync(client, owner, "پلن", 30, null, 100m);

        (await ListAsync(client, staff, "")).TotalCount.ShouldBe(1);
        (await GetAsync(client, staff, plan.Id)).Name.ShouldBe("پلن");
    }

    [Fact]
    public async Task ListPlans_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(PlansPath, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Update ----

    [Fact]
    public async Task UpdatePlan_ValidChange_Returns200WithANewVersion()
    {
        var (client, owner, _) = await ClientsAsync();
        var plan = await CreatePlanAsync(client, owner, "پلن", 30, 12, 900_000m);

        using var response = await UpdateAsync(client, owner, plan.Id, "سه ماهه", 90, null, 2_500_000m, plan.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<PlanResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        updated.Name.ShouldBe("سه ماهه");
        updated.DurationDays.ShouldBe(90);
        updated.SessionCount.ShouldBeNull();
        updated.Price.ShouldBe(2_500_000m);
        updated.Version.ShouldNotBe(plan.Version);
    }

    [Fact]
    public async Task UpdatePlan_KeepingItsOwnName_Succeeds()
    {
        var (client, owner, _) = await ClientsAsync();
        var plan = await CreatePlanAsync(client, owner, "ویژه", 30, null, 0m);

        using var response = await UpdateAsync(client, owner, plan.Id, $"و{ArabicYe}ژه", 60, null, 0m, plan.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdatePlan_TakingAnotherPlansName_Returns409()
    {
        var (client, owner, _) = await ClientsAsync();
        await CreatePlanAsync(client, owner, "ویژه", 30, null, 0m);
        var other = await CreatePlanAsync(client, owner, "عادی", 30, null, 0m);

        using var response = await UpdateAsync(client, owner, other.Id, "ویژه", 30, null, 0m, other.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Plans.NameAlreadyExists");
        (await StoredAsync(other.Id)).Name.ShouldBe("عادی");
    }

    [Fact]
    public async Task UpdatePlan_StaleVersion_Returns409AndChangesNothing()
    {
        var (client, owner, _) = await ClientsAsync();
        var plan = await CreatePlanAsync(client, owner, "پلن", 30, null, 0m);
        using var first = await UpdateAsync(client, owner, plan.Id, "پلن اول", 30, null, 0m, plan.Version);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var stale = await UpdateAsync(client, owner, plan.Id, "پلن دوم", 30, null, 0m, plan.Version);

        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await stale.ReadErrorCodeAsync()).ShouldBe("Plans.ChangedConcurrently");
        (await StoredAsync(plan.Id)).Name.ShouldBe("پلن اول");
    }

    [Fact]
    public async Task UpdatePlan_InvalidValue_Returns400()
    {
        var (client, owner, _) = await ClientsAsync();
        var plan = await CreatePlanAsync(client, owner, "پلن", 30, null, 0m);

        using var response = await UpdateAsync(client, owner, plan.Id, "پلن", 400, null, 0m, plan.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await StoredAsync(plan.Id)).DurationDays.ShouldBe(30);
    }

    [Fact]
    public async Task UpdatePlan_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await UpdateAsync(client, owner, Guid.CreateVersion7(), "پلن", 30, null, 0m, 0);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Plans.NotFound");
    }

    // ---- Get, activate, deactivate ----

    [Fact]
    public async Task GetPlan_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{PlansPath}/{Guid.CreateVersion7()}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Plans.NotFound");
    }

    [Fact]
    public async Task DeactivatePlan_ThenActivate_TogglesIsActive()
    {
        var (client, owner, _) = await ClientsAsync();
        var plan = await CreatePlanAsync(client, owner, "پلن", 30, null, 0m);

        using var deactivated = await PostAsync(client, owner, $"{PlansPath}/{plan.Id}/deactivate");
        deactivated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(plan.Id)).IsActive.ShouldBeFalse();

        using var activated = await PostAsync(client, owner, $"{PlansPath}/{plan.Id}/activate");
        activated.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(plan.Id)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task DeactivatePlan_AlreadyInactive_Returns200AndChangesNothing()
    {
        var (client, owner, _) = await ClientsAsync();
        var plan = await CreatePlanAsync(client, owner, "پلن", 30, null, 0m);
        using var first = await PostAsync(client, owner, $"{PlansPath}/{plan.Id}/deactivate");
        var version = (await StoredAsync(plan.Id)).Version;

        using var second = await PostAsync(client, owner, $"{PlansPath}/{plan.Id}/deactivate");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(plan.Id)).Version.ShouldBe(version, "nothing changed, so nothing was saved.");
    }

    [Fact]
    public async Task ActivatePlan_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await PostAsync(client, owner, $"{PlansPath}/{Guid.CreateVersion7()}/activate");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- List ----

    [Fact]
    public async Task ListPlans_Mixed_ActiveFirstThenByName()
    {
        var (client, owner, _) = await ClientsAsync();
        await CreatePlanAsync(client, owner, "ب", 30, null, 0m);
        var inactive = await CreatePlanAsync(client, owner, "الف قدیمی", 30, null, 0m);
        await CreatePlanAsync(client, owner, "پ", 30, null, 0m);
        await CreatePlanAsync(client, owner, "الف", 30, null, 0m);
        using var deactivated = await PostAsync(client, owner, $"{PlansPath}/{inactive.Id}/deactivate");

        var page = await ListAsync(client, owner, "");

        page.Items.Select(plan => plan.Name).ShouldBe(["الف", "ب", "پ", "الف قدیمی"]);
    }

    [Theory]
    [InlineData(true, "فعال")]
    [InlineData(false, "غیرفعال")]
    public async Task ListPlans_IsActiveFilter_ReturnsOnlyThoseRows(bool isActive, string expectedName)
    {
        var (client, owner, _) = await ClientsAsync();
        await CreatePlanAsync(client, owner, "فعال", 30, null, 0m);
        var inactive = await CreatePlanAsync(client, owner, "غیرفعال", 30, null, 0m);
        using var deactivated = await PostAsync(client, owner, $"{PlansPath}/{inactive.Id}/deactivate");

        var page = await ListAsync(client, owner, $"?isActive={isActive}");

        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Name.ShouldBe(expectedName);
    }

    [Fact]
    public async Task ListPlans_SecondPage_ReturnsTheRestWithTheTotal()
    {
        var (client, owner, _) = await ClientsAsync();
        foreach (var name in new[] { "الف", "ب", "پ" })
        {
            await CreatePlanAsync(client, owner, name, 30, null, 0m);
        }

        var page = await ListAsync(client, owner, "?page=2&pageSize=2");

        page.TotalCount.ShouldBe(3);
        page.Items.ShouldHaveSingleItem().Name.ShouldBe("پ");
    }

    [Fact]
    public async Task ListPlans_PageSizeOver100_Returns400()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{PlansPath}?pageSize=101", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Database constraints ----

    [Theory]
    [InlineData("'x'", 0, "NULL", 0, "ck_plans_duration_days_range")]
    [InlineData("'x'", 366, "NULL", 0, "ck_plans_duration_days_range")]
    [InlineData("'x'", 30, "0", 0, "ck_plans_session_count_range")]
    [InlineData("'x'", 30, "366", 0, "ck_plans_session_count_range")]
    [InlineData("'x'", 30, "NULL", -1, "ck_plans_price_not_negative")]
    [InlineData("'  '", 30, "NULL", 0, "ck_plans_name_not_blank")]
    public async Task Plans_InvalidRowInsertedDirectly_RejectedByACheckConstraint(
        string name, int durationDays, string sessionCount, int price, string expectedConstraint)
    {
        var exception = await Should.ThrowAsync<PostgresException>(() =>
            ExecuteSqlAsync(InsertSql(name, "'x'", durationDays, sessionCount, price)));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe(expectedConstraint);
    }

    [Fact]
    public async Task Plans_DuplicateNormalizedNameInsertedDirectly_RejectedByTheUniqueIndex()
    {
        await ExecuteSqlAsync(InsertSql("'a'", "'same'", 30, "NULL", 0));

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(InsertSql("'b'", "'same'", 30, "NULL", 0)));

        exception.SqlState.ShouldBe("23505");
        exception.ConstraintName.ShouldBe(PlanConstraints.UniqueName);
    }

    private static string InsertSql(string name, string normalizedName, int durationDays, string sessionCount, int price) =>
        $"""
        INSERT INTO plans (id, name, normalized_name, duration_days, session_count, price, is_active, created_at)
        VALUES ('{Guid.CreateVersion7()}', {name}, {normalizedName}, {durationDays}, {sessionCount}, {price}, true, now())
        """;

    // ---- Helpers ----

    /// <summary>One client, two tokens: the request's bearer decides who is asking.</summary>
    private async Task<(HttpClient Client, string Owner, string Staff)> ClientsAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client,
            await client.LoginForAccessTokenAsync("owner", TestUsers.Password),
            await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient client, string token, string name, int durationDays, int? sessionCount, decimal price) =>
        SendAsync(client, token, HttpMethod.Post, PlansPath, new { name, durationDays, sessionCount, price });

    private static async Task<PlanResponse> CreatePlanAsync(
        HttpClient client, string token, string name, int durationDays, int? sessionCount, decimal price)
    {
        using var response = await CreateAsync(client, token, name, durationDays, sessionCount, price);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PlanResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> UpdateAsync(
        HttpClient client, string token, Guid id, string name, int durationDays, int? sessionCount, decimal price, uint version) =>
        SendAsync(client, token, HttpMethod.Put, $"{PlansPath}/{id}", new { name, durationDays, sessionCount, price, version });

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string path) =>
        SendAsync(client, token, HttpMethod.Post, path, body: null);

    private static async Task<PlanResponse> GetAsync(HttpClient client, string token, Guid id)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"{PlansPath}/{id}", body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PlanResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<PagedResponse<PlanResponse>> ListAsync(HttpClient client, string token, string queryString)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, PlansPath + queryString, body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResponse<PlanResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private async Task<Gym.Domain.Plans.Plan> StoredAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Plans
            .AsNoTracking()
            .SingleAsync(plan => plan.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountPlansAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Plans.CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
