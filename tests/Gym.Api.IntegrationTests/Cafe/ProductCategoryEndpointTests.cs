using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Cafe;
using Gym.Application.Common.Paging;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Cafe;

/// <summary>
/// <c>/api/cafe/categories</c>. BUSINESS_RULES.md §8 and the permissions table in §1: staff have
/// no restriction in the cafe at all, so every endpoint here answers both roles. Switching a
/// category off is how a whole shelf becomes ناموجود.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ProductCategoryEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string CategoriesPath = "/api/cafe/categories";
    private const string ProductsPath = "/api/cafe/products";

    // ---- Create ----

    [Fact]
    public async Task CreateCategory_AsOwner_Returns201WithTheName()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, "  نوشیدنی ");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var category = (await response.Content.ReadFromJsonAsync<ProductCategoryResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{CategoriesPath}/{category.Id}");
        category.Name.ShouldBe("نوشیدنی");
    }

    [Fact]
    public async Task CreateCategory_AsStaff_Returns201()
    {
        // The rule the Owner widened on the day Phase 7 started: the cafe is front-desk work,
        // categories included.
        var (client, _, staff) = await ClientsAsync();

        using var response = await CreateAsync(client, staff, "نوشیدنی");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await CountCategoriesAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateCategory_AsOwner_IsActiveByDefault()
    {
        var (client, owner, _) = await ClientsAsync();

        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        category.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateCategory_DuplicateName_Returns409()
    {
        var (client, owner, _) = await ClientsAsync();
        await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await CreateAsync(client, owner, "نوشیدنی");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("ProductCategories.NameAlreadyExists");
    }

    [Fact]
    public async Task CreateCategory_SameNameInParallel_OneWinsAndTheRestGet409()
    {
        var (client, owner, _) = await ClientsAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CreateAsync(client, owner, "نوشیدنی")));

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

        using var response = await CreateAsync(client, owner, "   ");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty("name")[0].GetProperty("code").GetString()
            .ShouldBe("ProductCategories.NameRequired");
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateCategory_AsOwner_Returns200WithTheNewName()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await UpdateAsync(client, owner, category.Id, "نوشیدنی سرد", category.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<ProductCategoryResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().Name.ShouldBe("نوشیدنی سرد");
    }

    [Fact]
    public async Task UpdateCategory_AsStaff_Returns200()
    {
        var (client, owner, staff) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await UpdateAsync(client, staff, category.Id, "نوشیدنی سرد", category.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateCategory_StaleVersion_Returns409AndChangesNothing()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var staleVersion = category.Version;
        (await UpdateAsync(client, owner, category.Id, "نوشیدنی سرد", staleVersion)).Dispose();

        using var response = await UpdateAsync(client, owner, category.Id, "تنقلات", staleVersion);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("ProductCategories.ChangedConcurrently");
        (await StoredAsync(category.Id)).Name.ShouldBe("نوشیدنی سرد");
    }

    [Fact]
    public async Task UpdateCategory_TakingAnotherCategorysName_Returns409()
    {
        var (client, owner, _) = await ClientsAsync();
        await CreateCategoryAsync(client, owner, "تنقلات");
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await UpdateAsync(client, owner, category.Id, "تنقلات", category.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("ProductCategories.NameAlreadyExists");
    }

    [Fact]
    public async Task UpdateCategory_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await UpdateAsync(client, owner, Guid.CreateVersion7(), "نوشیدنی", version: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("ProductCategories.NotFound");
    }

    // ---- Delete ----

    [Fact]
    public async Task DeleteCategory_Empty_Returns204AndRemovesIt()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await DeleteAsync(client, owner, category.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountCategoriesAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task DeleteCategory_WithProducts_Returns409AndKeepsIt()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        await CreateProductAsync(client, owner, "آب معدنی", category.Id);

        using var response = await DeleteAsync(client, owner, category.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("ProductCategories.NotEmpty");
        (await CountCategoriesAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task DeleteCategory_WithOnlyInactiveProducts_Returns409()
    {
        // Deactivating a product does not remove it: an order still points at it.
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var product = await CreateProductAsync(client, owner, "آب معدنی", category.Id);
        using (var deactivated = await SendAsync(
                   client, owner, HttpMethod.Post, $"{ProductsPath}/{product.Id}/deactivate", body: null))
        {
            deactivated.EnsureSuccessStatusCode();
        }

        using var response = await DeleteAsync(client, owner, category.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("ProductCategories.NotEmpty");
    }

    [Fact]
    public async Task DeleteCategory_AsStaff_Returns204()
    {
        var (client, owner, staff) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await DeleteAsync(client, staff, category.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await CountCategoriesAsync()).ShouldBe(0);
    }

    // ---- Switch on and off ----

    [Fact]
    public async Task DeactivateCategory_AsStaff_Returns200AndMarksItUnavailable()
    {
        var (client, owner, staff) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await SendAsync(
            client, staff, HttpMethod.Post, $"{CategoriesPath}/{category.Id}/deactivate", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(category.Id)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task ActivateCategory_InactiveCategory_Returns200AndPutsItBack()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        await DeactivateCategoryAsync(client, owner, category.Id);

        using var response = await SendAsync(
            client, owner, HttpMethod.Post, $"{CategoriesPath}/{category.Id}/activate", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(category.Id)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task DeactivateCategory_AlreadyInactive_Returns200AndChangesNothing()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        await DeactivateCategoryAsync(client, owner, category.Id);
        var version = (await StoredAsync(category.Id)).Version;

        using var response = await SendAsync(
            client, owner, HttpMethod.Post, $"{CategoriesPath}/{category.Id}/deactivate", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(category.Id)).Version.ShouldBe(version, "nothing changed, so nothing was saved.");
    }

    [Fact]
    public async Task DeactivateCategory_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await SendAsync(
            client, owner, HttpMethod.Post, $"{CategoriesPath}/{Guid.CreateVersion7()}/deactivate", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("ProductCategories.NotFound");
    }

    [Fact]
    public async Task DeactivateCategory_ThenDelete_StillWorksWhileEmpty()
    {
        // Switching off is "not today"; deleting is "this was a mistake". One does not block the
        // other (BUSINESS_RULES.md §8).
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        await DeactivateCategoryAsync(client, owner, category.Id);

        using var response = await DeleteAsync(client, owner, category.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteCategory_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await DeleteAsync(client, owner, Guid.CreateVersion7());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("ProductCategories.NotFound");
    }

    // ---- List ----

    [Fact]
    public async Task ListCategories_AsStaff_Returns200OrderedByName()
    {
        var (client, owner, staff) = await ClientsAsync();
        await CreateCategoryAsync(client, owner, "نوشیدنی");
        await CreateCategoryAsync(client, owner, "تنقلات");

        using var response = await SendAsync(client, staff, HttpMethod.Get, CategoriesPath, body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<ProductCategoryResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        page.Items.Select(category => category.Name).ShouldBe(["تنقلات", "نوشیدنی"]);
    }

    [Fact]
    public async Task ListCategories_ActiveOnly_LeavesOutTheSwitchedOffShelf()
    {
        // What the till asks for: a ناموجود heading is never offered.
        var (client, owner, _) = await ClientsAsync();
        await CreateCategoryAsync(client, owner, "نوشیدنی");
        var snacks = await CreateCategoryAsync(client, owner, "تنقلات");
        await DeactivateCategoryAsync(client, owner, snacks.Id);

        using var response = await SendAsync(
            client, owner, HttpMethod.Get, $"{CategoriesPath}?isActive=true", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<ProductCategoryResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Name.ShouldBe("نوشیدنی");
    }

    [Fact]
    public async Task ListCategories_Unfiltered_PutsTheSwitchedOffOnesLast()
    {
        var (client, owner, _) = await ClientsAsync();
        var drinks = await CreateCategoryAsync(client, owner, "نوشیدنی");
        await CreateCategoryAsync(client, owner, "تنقلات");
        await DeactivateCategoryAsync(client, owner, drinks.Id);

        using var response = await SendAsync(client, owner, HttpMethod.Get, CategoriesPath, body: null);

        var page = (await response.Content.ReadFromJsonAsync<PagedResponse<ProductCategoryResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        page.Items.Select(category => category.Name).ShouldBe(["تنقلات", "نوشیدنی"]);
    }

    [Fact]
    public async Task ListCategories_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(CategoriesPath, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListCategories_PageSizeOver100_Returns400()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{CategoriesPath}?pageSize=101", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Database constraints ----

    [Fact]
    public async Task ProductCategories_DuplicateNameInsertedDirectly_RejectedByTheUniqueIndex()
    {
        await ExecuteSqlAsync(InsertSql(Guid.CreateVersion7(), "نوشیدنی"));

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync(InsertSql(Guid.CreateVersion7(), "نوشیدنی")));

        exception.SqlState.ShouldBe("23505");
        exception.ConstraintName.ShouldBe(CafeConstraints.UniqueCategoryName);
    }

    [Fact]
    public async Task ProductCategories_BlankNameInsertedDirectly_RejectedByACheckConstraint()
    {
        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync(InsertSql(Guid.CreateVersion7(), "  ")));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe("ck_product_categories_name_not_blank");
    }

    private static string InsertSql(Guid id, string name) =>
        $"""
        INSERT INTO product_categories (id, name, normalized_name, is_active, created_at)
        VALUES ('{id}', '{name}', '{name}', true, now())
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

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string token, string name) =>
        SendAsync(client, token, HttpMethod.Post, CategoriesPath, new { name });

    private static async Task<ProductCategoryResponse> CreateCategoryAsync(HttpClient client, string token, string name)
    {
        using var response = await CreateAsync(client, token, name);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductCategoryResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<ProductResponse> CreateProductAsync(
        HttpClient client, string token, string name, Guid categoryId)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, ProductsPath, new { name, categoryId, price = "15000" });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> UpdateAsync(
        HttpClient client, string token, Guid id, string name, uint version) =>
            SendAsync(client, token, HttpMethod.Put, $"{CategoriesPath}/{id}", new { name, version });

    private static Task<HttpResponseMessage> DeleteAsync(HttpClient client, string token, Guid id) =>
        SendAsync(client, token, HttpMethod.Delete, $"{CategoriesPath}/{id}", body: null);

    private static async Task DeactivateCategoryAsync(HttpClient client, string token, Guid id)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"{CategoriesPath}/{id}/deactivate", body: null);

        response.EnsureSuccessStatusCode();
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

    private async Task<Gym.Domain.Cafe.ProductCategory> StoredAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductCategories
            .AsNoTracking()
            .SingleAsync(category => category.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountCategoriesAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().ProductCategories
            .CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
