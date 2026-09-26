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
/// <c>/api/cafe/products</c>. BUSINESS_RULES.md §8 and the permissions table in §1: the cafe is
/// front-desk work and staff have no restriction in it. There is no stock anywhere here — the
/// Owner decided the gym does not count what is in the fridge, so the موجود / ناموجود switch is
/// the only thing that says whether something can be bought today.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ProductEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string ProductsPath = "/api/cafe/products";
    private const string CategoriesPath = "/api/cafe/categories";

    // ---- Create ----

    [Fact]
    public async Task CreateProduct_AsOwner_Returns201WithTheValues()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await CreateAsync(client, owner, "  آب معدنی ", category.Id, 15_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var product = (await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{ProductsPath}/{product.Id}");
        product.Name.ShouldBe("آب معدنی");
        product.CategoryId.ShouldBe(category.Id);
        product.CategoryName.ShouldBe("نوشیدنی");
        product.Price.ShouldBe(15_000m);
        product.IsActive.ShouldBeTrue();
        product.CategoryIsActive.ShouldBeTrue();
        product.IsSellable.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateProduct_AsStaff_Returns201()
    {
        // The rule the Owner changed when Phase 7 started: this is front-desk work.
        var (client, owner, staff) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await CreateAsync(client, staff, "آب معدنی", category.Id, 15_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateProduct_DuplicateNameInAnotherCategory_Returns409()
    {
        // Names are unique across the whole cafe, not per category: two identical names at the
        // till would be a coin flip.
        var (client, owner, _) = await ClientsAsync();
        var drinks = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var snacks = await CreateCategoryAsync(client, owner, "تنقلات");
        await CreateProductAsync(client, owner, "آب معدنی", drinks.Id, 15_000m);

        using var response = await CreateAsync(client, owner, "آب معدنی", snacks.Id, 18_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Products.NameAlreadyExists");
    }

    [Fact]
    public async Task CreateProduct_NameDifferingOnlyByArabicYe_Returns409()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        await CreateProductAsync(client, owner, "چای", category.Id, 10_000m);

        using var response = await CreateAsync(client, owner, $"چا{(char)0x064A}", category.Id, 10_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Products.NameAlreadyExists");
    }

    [Fact]
    public async Task CreateProduct_SameNameInParallel_OneWinsAndTheRestGet409()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(_ => CreateAsync(client, owner, "آب معدنی", category.Id, 15_000m)));

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
    public async Task CreateProduct_UnknownCategory_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, "آب معدنی", Guid.CreateVersion7(), 15_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Products.CategoryNotFound");
        (await CountProductsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateProduct_PriceWithThreeDecimals_Returns400WithFieldCode()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await CreateAsync(client, owner, "آب معدنی", category.Id, 15_000.005m);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldCodeAsync(response, "price")).ShouldBe("Products.PriceTooManyDecimals");
        (await CountProductsAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateProduct_NegativePrice_Returns400WithFieldCode()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await CreateAsync(client, owner, "آب معدنی", category.Id, -1m);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldCodeAsync(response, "price")).ShouldBe("Products.PriceNegative");
    }

    [Fact]
    public async Task CreateProduct_BlankName_Returns400WithFieldCode()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await CreateAsync(client, owner, "   ", category.Id, 15_000m);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldCodeAsync(response, "name")).ShouldBe("Products.NameRequired");
    }

    [Fact]
    public async Task CreateProduct_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            ProductsPath,
            new { name = "آب معدنی", categoryId = Guid.CreateVersion7(), price = "15000" },
            TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Update ----

    [Fact]
    public async Task UpdateProduct_AsStaff_Returns200WithTheNewValues()
    {
        var (client, owner, staff) = await ClientsAsync();
        var drinks = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var snacks = await CreateCategoryAsync(client, owner, "تنقلات");
        var product = await CreateProductAsync(client, owner, "آب معدنی", drinks.Id, 15_000m);

        using var response = await UpdateAsync(client, staff, product.Id, "کیک", snacks.Id, 25_000m, product.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        updated.Name.ShouldBe("کیک");
        updated.CategoryId.ShouldBe(snacks.Id);
        updated.CategoryName.ShouldBe("تنقلات");
        updated.Price.ShouldBe(25_000m);
    }

    [Fact]
    public async Task UpdateProduct_StaleVersion_Returns409AndChangesNothing()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var product = await CreateProductAsync(client, owner, "آب معدنی", category.Id, 15_000m);
        var staleVersion = product.Version;
        (await UpdateAsync(client, owner, product.Id, "آب معدنی", category.Id, 16_000m, staleVersion)).Dispose();

        using var response = await UpdateAsync(client, owner, product.Id, "آب معدنی", category.Id, 99_000m, staleVersion);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Products.ChangedConcurrently");
        (await StoredAsync(product.Id)).Price.ShouldBe(16_000m);
    }

    [Fact]
    public async Task UpdateProduct_KeepingItsOwnName_Succeeds()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var product = await CreateProductAsync(client, owner, "آب معدنی", category.Id, 15_000m);

        using var response = await UpdateAsync(client, owner, product.Id, "آب معدنی", category.Id, 16_000m, product.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateProduct_TakingAnotherProductsName_Returns409()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        await CreateProductAsync(client, owner, "چای", category.Id, 10_000m);
        var product = await CreateProductAsync(client, owner, "آب معدنی", category.Id, 15_000m);

        using var response = await UpdateAsync(client, owner, product.Id, "چای", category.Id, 15_000m, product.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Products.NameAlreadyExists");
    }

    [Fact]
    public async Task UpdateProduct_UnknownCategory_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var product = await CreateProductAsync(client, owner, "آب معدنی", category.Id, 15_000m);

        using var response = await UpdateAsync(
            client, owner, product.Id, "آب معدنی", Guid.CreateVersion7(), 15_000m, product.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Products.CategoryNotFound");
        (await StoredAsync(product.Id)).CategoryId.ShouldBe(category.Id);
    }

    [Fact]
    public async Task UpdateProduct_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        using var response = await UpdateAsync(
            client, owner, Guid.CreateVersion7(), "آب معدنی", category.Id, 15_000m, version: 1);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Products.NotFound");
    }

    // ---- Activate / deactivate ----

    [Fact]
    public async Task DeactivateProduct_AsStaff_Returns200AndTakesItOffTheList()
    {
        var (client, owner, staff) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var product = await CreateProductAsync(client, owner, "آب معدنی", category.Id, 15_000m);

        using var response = await PostAsync(client, staff, $"{ProductsPath}/{product.Id}/deactivate");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(product.Id)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task ActivateProduct_InactiveProduct_Returns200AndPutsItBack()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var product = await CreateProductAsync(client, owner, "آب معدنی", category.Id, 15_000m);
        (await PostAsync(client, owner, $"{ProductsPath}/{product.Id}/deactivate")).Dispose();

        using var response = await PostAsync(client, owner, $"{ProductsPath}/{product.Id}/activate");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(product.Id)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task DeactivateProduct_AlreadyInactive_Returns200AndChangesNothing()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var product = await CreateProductAsync(client, owner, "آب معدنی", category.Id, 15_000m);
        (await PostAsync(client, owner, $"{ProductsPath}/{product.Id}/deactivate")).Dispose();
        var version = (await StoredAsync(product.Id)).Version;

        using var response = await PostAsync(client, owner, $"{ProductsPath}/{product.Id}/deactivate");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(product.Id)).Version.ShouldBe(version, "nothing changed, so nothing was saved.");
    }

    [Fact]
    public async Task DeactivateProduct_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await PostAsync(client, owner, $"{ProductsPath}/{Guid.CreateVersion7()}/deactivate");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeactivateCategory_ProductStaysActiveButStopsBeingSellable()
    {
        // BUSINESS_RULES.md §8: switching a shelf off takes its products out of the till in one
        // action, and switching it back on restores exactly the ones that were switched on.
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var product = await CreateProductAsync(client, owner, "آب معدنی", category.Id, 15_000m);

        await DeactivateCategoryAsync(client, owner, category.Id);

        (await StoredAsync(product.Id)).IsActive.ShouldBeTrue("the product's own switch is untouched.");
        var found = await GetProductAsync(client, owner, product.Id);
        found.CategoryIsActive.ShouldBeFalse();
        found.IsSellable.ShouldBeFalse();
    }

    // ---- Read ----

    [Fact]
    public async Task GetProduct_AsStaff_Returns200WithTheCategoryName()
    {
        var (client, owner, staff) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var product = await CreateProductAsync(client, owner, "آب معدنی", category.Id, 15_000m);

        using var response = await SendAsync(client, staff, HttpMethod.Get, $"{ProductsPath}/{product.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var found = (await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        found.Id.ShouldBe(product.Id);
        found.CategoryName.ShouldBe("نوشیدنی");
    }

    [Fact]
    public async Task GetProduct_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await SendAsync(
            client, owner, HttpMethod.Get, $"{ProductsPath}/{Guid.CreateVersion7()}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Products.NotFound");
    }

    [Fact]
    public async Task ListProducts_Mixed_ActiveFirstThenByCategoryAndName()
    {
        var (client, owner, _) = await ClientsAsync();
        var drinks = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var snacks = await CreateCategoryAsync(client, owner, "تنقلات");
        await CreateProductAsync(client, owner, "چای", drinks.Id, 10_000m);
        await CreateProductAsync(client, owner, "آب معدنی", drinks.Id, 15_000m);
        var cake = await CreateProductAsync(client, owner, "کیک", snacks.Id, 25_000m);
        (await PostAsync(client, owner, $"{ProductsPath}/{cake.Id}/deactivate")).Dispose();

        var page = await ListAsync(client, owner, "");

        // "تنقلات" sorts before "نوشیدنی", but the inactive cake is pushed to the end anyway.
        page.Items.Select(product => product.Name).ShouldBe(["آب معدنی", "چای", "کیک"]);
    }

    [Fact]
    public async Task ListProducts_FilteredByCategory_ReturnsOnlyThatCategory()
    {
        var (client, owner, _) = await ClientsAsync();
        var drinks = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var snacks = await CreateCategoryAsync(client, owner, "تنقلات");
        await CreateProductAsync(client, owner, "آب معدنی", drinks.Id, 15_000m);
        await CreateProductAsync(client, owner, "کیک", snacks.Id, 25_000m);

        var page = await ListAsync(client, owner, $"?categoryId={drinks.Id}");

        page.Items.ShouldHaveSingleItem().Name.ShouldBe("آب معدنی");
    }

    [Fact]
    public async Task ListProducts_FilteredByActive_LeavesOutTheRest()
    {
        var (client, owner, staff) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        await CreateProductAsync(client, owner, "آب معدنی", category.Id, 15_000m);
        var tea = await CreateProductAsync(client, owner, "چای", category.Id, 10_000m);
        (await PostAsync(client, owner, $"{ProductsPath}/{tea.Id}/deactivate")).Dispose();

        var page = await ListAsync(client, staff, "?isActive=true");

        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Name.ShouldBe("آب معدنی");
    }

    [Fact]
    public async Task ListProducts_SellableOnly_LeavesOutProductsOfASwitchedOffCategory()
    {
        // The till and the product search on an order ask this question, and a ناموجود item must
        // never be offered — whichever switch made it ناموجود.
        var (client, owner, staff) = await ClientsAsync();
        var drinks = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var snacks = await CreateCategoryAsync(client, owner, "تنقلات");
        await CreateProductAsync(client, owner, "آب معدنی", drinks.Id, 15_000m);
        await CreateProductAsync(client, owner, "کیک", snacks.Id, 25_000m);
        await DeactivateCategoryAsync(client, owner, snacks.Id);

        var page = await ListAsync(client, staff, "?isActive=true");

        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Name.ShouldBe("آب معدنی");
    }

    [Fact]
    public async Task ListProducts_NotSellable_IncludesBothKindsOfSwitchedOff()
    {
        var (client, owner, _) = await ClientsAsync();
        var drinks = await CreateCategoryAsync(client, owner, "نوشیدنی");
        var snacks = await CreateCategoryAsync(client, owner, "تنقلات");
        var tea = await CreateProductAsync(client, owner, "چای", drinks.Id, 10_000m);
        await CreateProductAsync(client, owner, "کیک", snacks.Id, 25_000m);
        (await PostAsync(client, owner, $"{ProductsPath}/{tea.Id}/deactivate")).Dispose();
        await DeactivateCategoryAsync(client, owner, snacks.Id);

        var page = await ListAsync(client, owner, "?isActive=false");

        page.TotalCount.ShouldBe(2);
        page.Items.Select(product => product.Name).ShouldBe(["کیک", "چای"]);
    }

    [Fact]
    public async Task ListProducts_SwitchedCategoryBackOn_RestoresOnlyTheProductsThatWereOn()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        await CreateProductAsync(client, owner, "آب معدنی", category.Id, 15_000m);
        var tea = await CreateProductAsync(client, owner, "چای", category.Id, 10_000m);
        (await PostAsync(client, owner, $"{ProductsPath}/{tea.Id}/deactivate")).Dispose();
        await DeactivateCategoryAsync(client, owner, category.Id);

        using (var activated = await SendAsync(
                   client, owner, HttpMethod.Post, $"/api/cafe/categories/{category.Id}/activate", body: null))
        {
            activated.EnsureSuccessStatusCode();
        }

        var page = await ListAsync(client, owner, "?isActive=true");

        page.Items.ShouldHaveSingleItem().Name.ShouldBe("آب معدنی");
    }

    [Fact]
    public async Task ListProducts_SecondPage_ReturnsTheRestWithTheTotal()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        foreach (var name in new[] { "آب معدنی", "چای", "دوغ" })
        {
            await CreateProductAsync(client, owner, name, category.Id, 10_000m);
        }

        var page = await ListAsync(client, owner, "?page=2&pageSize=2");

        page.TotalCount.ShouldBe(3);
        page.Items.ShouldHaveSingleItem().Name.ShouldBe("دوغ");
    }

    [Fact]
    public async Task ListProducts_PageSizeOver100_Returns400()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{ProductsPath}?pageSize=101", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Database constraints ----

    [Fact]
    public async Task Products_NegativePriceInsertedDirectly_RejectedByACheckConstraint()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync(InsertSql(Guid.CreateVersion7(), "آب معدنی", category.Id, "-1")));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe("ck_products_price_not_negative");
    }

    [Fact]
    public async Task Products_DuplicateNameInsertedDirectly_RejectedByTheUniqueIndex()
    {
        var (client, owner, _) = await ClientsAsync();
        var category = await CreateCategoryAsync(client, owner, "نوشیدنی");
        await ExecuteSqlAsync(InsertSql(Guid.CreateVersion7(), "آب معدنی", category.Id, "15000"));

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync(InsertSql(Guid.CreateVersion7(), "آب معدنی", category.Id, "15000")));

        exception.SqlState.ShouldBe("23505");
        exception.ConstraintName.ShouldBe(CafeConstraints.UniqueProductName);
    }

    [Fact]
    public async Task Products_UnknownCategoryInsertedDirectly_RejectedByTheForeignKey()
    {
        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync(InsertSql(Guid.CreateVersion7(), "آب معدنی", Guid.CreateVersion7(), "15000")));

        exception.SqlState.ShouldBe("23503");
        exception.ConstraintName.ShouldBe(CafeConstraints.ProductCategoryForeignKey);
    }

    private static string InsertSql(Guid id, string name, Guid categoryId, string price) =>
        $"""
        INSERT INTO products (id, name, normalized_name, category_id, price, is_active, created_at)
        VALUES ('{id}', '{name}', '{name}', '{categoryId}', {price}, true, now())
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

    private static async Task<ProductCategoryResponse> CreateCategoryAsync(HttpClient client, string token, string name)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, CategoriesPath, new { name });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductCategoryResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>
    /// The price goes over the wire as a string, the way the frontend sends money: the allowed
    /// range has more digits than a JavaScript number holds exactly (docs/ARCHITECTURE.md).
    /// </summary>
    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient client, string token, string name, Guid categoryId, decimal price) =>
            SendAsync(
                client,
                token,
                HttpMethod.Post,
                ProductsPath,
                new { name, categoryId, price = price.ToString(System.Globalization.CultureInfo.InvariantCulture) });

    private static async Task<ProductResponse> CreateProductAsync(
        HttpClient client, string token, string name, Guid categoryId, decimal price)
    {
        using var response = await CreateAsync(client, token, name, categoryId, price);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> UpdateAsync(
        HttpClient client, string token, Guid id, string name, Guid categoryId, decimal price, uint version) =>
            SendAsync(
                client,
                token,
                HttpMethod.Put,
                $"{ProductsPath}/{id}",
                new
                {
                    name,
                    categoryId,
                    price = price.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    version,
                });

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string path) =>
        SendAsync(client, token, HttpMethod.Post, path, body: null);

    private static async Task DeactivateCategoryAsync(HttpClient client, string token, Guid id)
    {
        using var response = await SendAsync(
            client, token, HttpMethod.Post, $"{CategoriesPath}/{id}/deactivate", body: null);

        response.EnsureSuccessStatusCode();
    }

    private static async Task<ProductResponse> GetProductAsync(HttpClient client, string token, Guid id)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, $"{ProductsPath}/{id}", body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ProductResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<PagedResponse<ProductResponse>> ListAsync(
        HttpClient client, string token, string queryString)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, ProductsPath + queryString, body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResponse<ProductResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
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

    private static async Task<string?> FieldCodeAsync(HttpResponseMessage response, string field)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString();
    }

    private async Task<Gym.Domain.Cafe.Product> StoredAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products
            .AsNoTracking()
            .SingleAsync(product => product.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountProductsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Products
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
