using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Paging;
using Gym.Application.Expenses;
using Gym.Domain.Expenses;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence.Seed;

namespace Gym.Api.IntegrationTests.Expenses;

/// <summary>
/// <c>/api/expenses/categories</c>. BUSINESS_RULES.md §9: eight categories arrive with the
/// migration, the Owner adds and renames, and nothing is deleted or switched off. §1: Owner only.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ExpenseCategoryEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string CategoriesPath = "/api/expenses/categories";
    private const char ArabicYe = (char)0x064A;

    private static readonly string[] SeededNames =
        ["اجاره", "حقوق", "برق", "آب", "تجهیزات", "تعمیر و نگهداری", "خرید بوفه", "سایر"];

    // ---- Seed ----

    [Fact]
    public void Migration_WhenApplied_SeedsTheEightCategoriesWithTheirPersianNames()
    {
        // Read by the fixture right after migrating, before any reset put rows back: this is the
        // migration's own work, not the test harness's.
        var seeded = Fixture.ExpenseCategoriesAfterMigration;

        seeded.Select(category => category.Name).ShouldBe(SeededNames, ignoreOrder: true);
        seeded.Select(category => category.Id)
            .ShouldBe(ExpenseCategorySeed.All.Select(seed => seed.Id), ignoreOrder: true);
        seeded.ShouldAllBe(category => category.NormalizedName == ExpenseCategory.Normalize(category.Name));
    }

    [Fact]
    public async Task ListCategories_AsOwner_ReturnsTheSeededCategories()
    {
        var (client, owner, _) = await ClientsAsync();

        var page = await ListAsync(client, owner);

        page.TotalCount.ShouldBe(8);
        page.Items.Select(category => category.Name).ShouldBe(SeededNames, ignoreOrder: true);
    }

    [Fact]
    public async Task ListCategories_AsStaff_Returns403()
    {
        var (client, _, staff) = await ClientsAsync();

        using var response = await SendAsync(client, staff, HttpMethod.Get, CategoriesPath, body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- Create ----

    [Fact]
    public async Task CreateCategory_AsOwner_Returns201AndIsListed()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, "  بیمه ");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var category = (await response.Content.ReadFromJsonAsync<ExpenseCategoryResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{CategoriesPath}/{category.Id}");
        category.Name.ShouldBe("بیمه");
        (await ListAsync(client, owner)).TotalCount.ShouldBe(9);
    }

    [Fact]
    public async Task CreateCategory_AsStaff_Returns403AndCreatesNothing()
    {
        var (client, owner, staff) = await ClientsAsync();

        using var response = await CreateAsync(client, staff, "بیمه");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ListAsync(client, owner)).TotalCount.ShouldBe(8);
    }

    [Fact]
    public async Task CreateCategory_SeededNameTypedWithArabicYe_Returns409()
    {
        // "سایر" with an Arabic ye looks the same on screen; the normalized unique index says so too.
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, $"سا{ArabicYe}ر");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("ExpenseCategories.NameAlreadyExists");
    }

    [Fact]
    public async Task CreateCategory_SameNameInParallel_OneWinsAndTheRestGet409()
    {
        var (client, owner, _) = await ClientsAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CreateAsync(client, owner, "بیمه")));

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

    [Fact]
    public async Task CreateCategory_BlankName_Returns400WithFieldCode()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, "  ");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty("name")[0].GetProperty("code").GetString()
            .ShouldBe("ExpenseCategories.NameRequired");
    }

    // ---- Rename ----

    [Fact]
    public async Task UpdateCategory_SeededCategory_RenamesIt()
    {
        var (client, owner, _) = await ClientsAsync();
        var rent = (await ListAsync(client, owner)).Items.Single(category => category.Name == "اجاره");

        using var response = await UpdateAsync(client, owner, rent.Id, "اجاره سالن", rent.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ExpenseCategoryResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().Name.ShouldBe("اجاره سالن");
    }

    [Fact]
    public async Task UpdateCategory_AsStaff_Returns403()
    {
        var (client, owner, staff) = await ClientsAsync();
        var rent = (await ListAsync(client, owner)).Items.Single(category => category.Name == "اجاره");

        using var response = await UpdateAsync(client, staff, rent.Id, "اجاره سالن", rent.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateCategory_StaleVersion_Returns409AndKeepsTheNewerName()
    {
        var (client, owner, _) = await ClientsAsync();
        var rent = (await ListAsync(client, owner)).Items.Single(category => category.Name == "اجاره");
        (await UpdateAsync(client, owner, rent.Id, "اجاره سالن", rent.Version)).Dispose();

        using var response = await UpdateAsync(client, owner, rent.Id, "کرایه", rent.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("ExpenseCategories.ChangedConcurrently");
        (await ListAsync(client, owner)).Items.ShouldContain(category => category.Name == "اجاره سالن");
    }

    [Fact]
    public async Task UpdateCategory_AnotherCategorysName_Returns409()
    {
        var (client, owner, _) = await ClientsAsync();
        var rent = (await ListAsync(client, owner)).Items.Single(category => category.Name == "اجاره");

        using var response = await UpdateAsync(client, owner, rent.Id, "سایر", rent.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("ExpenseCategories.NameAlreadyExists");
    }

    [Fact]
    public async Task UpdateCategory_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await UpdateAsync(client, owner, Guid.CreateVersion7(), "بیمه", version: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("ExpenseCategories.NotFound");
    }

    [Fact]
    public async Task DeleteCategory_AsOwner_HasNoEndpoint()
    {
        // BUSINESS_RULES.md §9: expenses and reports point at a category, so it is never deleted.
        var (client, owner, _) = await ClientsAsync();
        var rent = (await ListAsync(client, owner)).Items.Single(category => category.Name == "اجاره");

        using var response = await SendAsync(client, owner, HttpMethod.Delete, $"{CategoriesPath}/{rent.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await ListAsync(client, owner)).TotalCount.ShouldBe(8);
    }

    private async Task<(HttpClient Client, string Owner, string Staff)> ClientsAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client,
            await client.LoginForAccessTokenAsync("owner", TestUsers.Password),
            await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private static async Task<PagedResponse<ExpenseCategoryResponse>> ListAsync(HttpClient client, string token)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"{CategoriesPath}?pageSize=100", body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResponse<ExpenseCategoryResponse>>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string token, string name) =>
        SendAsync(client, token, HttpMethod.Post, CategoriesPath, new { name });

    private static Task<HttpResponseMessage> UpdateAsync(
        HttpClient client, string token, Guid id, string name, uint version) =>
            SendAsync(client, token, HttpMethod.Put, $"{CategoriesPath}/{id}", new { name, version });

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
}
