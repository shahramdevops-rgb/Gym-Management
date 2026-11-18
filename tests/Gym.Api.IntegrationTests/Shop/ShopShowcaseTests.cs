using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using Gym.Api.Configuration;
using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Shop;
using Gym.Domain.Shop;
using Gym.Api.IntegrationTests.SiteAuth;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

using SkiaSharp;

namespace Gym.Api.IntegrationTests.Shop;

/// <summary>
/// The shop's showcase on the gym's site (BUSINESS_RULES.md §16 <i>The showcase</i>, task 16.4):
/// anyone reads the shown products without signing in; a hidden product, the number in stock and
/// everything the shop admin edits with never leave the server.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ShopShowcaseTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string ShowcasePath = "/api/site/shop/showcase";
    private const string ShowcaseProductsPath = "/api/site/shop/products";
    private const string ShowcasePhotosPath = "/api/site/shop/photos";

    private const string CategoriesPath = "/api/shop/categories";
    private const string ProductsPath = "/api/shop/products";
    private const string ContactsPath = "/api/shop/contacts";

    [Fact]
    public async Task Showcase_WithoutSigningIn_ListsTheShownProductsNewestFirst()
    {
        var (admin, token) = await ShopAdminAsync();
        var supplements = await CreateCategoryAsync(admin, token, "مکمل");
        var wear = await CreateCategoryAsync(admin, token, "پوشاک");
        var creatine = await CreateProductAsync(admin, token, supplements.Id, "کراتین", Variant("", 900_000m, 3));
        var hidden = await CreateProductAsync(admin, token, supplements.Id, "پروتئین پنهان", isVisible: false, Variant("", 500_000m, 9));
        var shirt = await CreateProductAsync(
            admin, token, wear.Id, "تی‌شرت", Variant("مدیوم", 400_000m, 0), Variant("لارج", 450_000m, 2));
        var photo = await AddPhotoAsync(admin, token, creatine.Id);

        using var anonymous = Fixture.CreateClient();
        var showcase = await ReadAsync<ShowcaseResponse>(anonymous, ShowcasePath);

        showcase.Products.Select(product => product.Id).ShouldBe([shirt.Id, creatine.Id]);
        showcase.Products.ShouldNotContain(product => product.Id == hidden.Id);
        showcase.Categories.Select(category => category.Name).ShouldBe(["پوشاک", "مکمل"]);

        var shirtCard = showcase.Products[0];
        shirtCard.Name.ShouldBe("تی‌شرت");
        shirtCard.CategoryId.ShouldBe(wear.Id);
        shirtCard.MinPrice.ShouldBe(400_000m);
        shirtCard.MaxPrice.ShouldBe(450_000m);
        shirtCard.InStock.ShouldBeTrue();
        shirtCard.MainPhotoId.ShouldBeNull();
        showcase.Products[1].MainPhotoId.ShouldBe(photo.Id);
    }

    [Fact]
    public async Task Showcase_AProductWithNoStockAtAll_IsShownAsOutOfStock()
    {
        var (admin, token) = await ShopAdminAsync();
        var category = await CreateCategoryAsync(admin, token, "مکمل");
        await CreateProductAsync(admin, token, category.Id, "کراتین", Variant("۳۰۰ گرم", 900_000m, 0), Variant("۵۰۰ گرم", 1_300_000m, 0));

        using var anonymous = Fixture.CreateClient();
        var showcase = await ReadAsync<ShowcaseResponse>(anonymous, ShowcasePath);

        showcase.Products.ShouldHaveSingleItem().InStock.ShouldBeFalse();
    }

    [Fact]
    public async Task Showcase_ACategoryWithNothingShown_IsLeftOut()
    {
        var (admin, token) = await ShopAdminAsync();
        var empty = await CreateCategoryAsync(admin, token, "عطر");
        var allHidden = await CreateCategoryAsync(admin, token, "پوشاک");
        await CreateProductAsync(admin, token, allHidden.Id, "تی‌شرت", isVisible: false, Variant("", 400_000m, 2));

        using var anonymous = Fixture.CreateClient();
        var showcase = await ReadAsync<ShowcaseResponse>(anonymous, ShowcasePath);

        showcase.Products.ShouldBeEmpty();
        showcase.Categories.ShouldBeEmpty();
        empty.ProductCount.ShouldBe(0);
    }

    [Fact]
    public async Task Showcase_TheAnswer_NeverCarriesTheStockNumberOrWhatTheAdminEditsWith()
    {
        var (admin, token) = await ShopAdminAsync();
        var category = await CreateCategoryAsync(admin, token, "مکمل");
        var product = await CreateProductAsync(admin, token, category.Id, "کراتین", Variant("", 900_000m, 37));

        using var anonymous = Fixture.CreateClient();
        var list = await anonymous.GetStringAsync(ShowcasePath, TestContext.Current.CancellationToken);
        var one = await anonymous.GetStringAsync($"{ShowcaseProductsPath}/{product.Id}", TestContext.Current.CancellationToken);

        foreach (var json in new[] { list, one })
        {
            json.ShouldNotContain("37");
            json.ShouldNotContain("\"stock\"", Case.Insensitive);
            json.ShouldNotContain("totalStock", Case.Insensitive);
            json.ShouldNotContain("isVisible", Case.Insensitive);
            json.ShouldNotContain("version", Case.Insensitive);
        }
    }

    [Fact]
    public async Task Showcase_TheContactWays_AreTheShopsOwn()
    {
        var (admin, token) = await ShopAdminAsync();
        using var read = await SendAsync(admin, token, HttpMethod.Get, ContactsPath);
        var version = (await read.Content.ReadFromJsonAsync<ShopContactsResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull().Version;
        using var saved = await SendAsync(admin, token, HttpMethod.Put, ContactsPath,
            new { mobilePhone = "09121234567", telegram = "@pasargad_shop", version });
        saved.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var anonymous = Fixture.CreateClient();
        var contacts = (await ReadAsync<ShowcaseResponse>(anonymous, ShowcasePath)).Contacts;

        contacts.MobilePhone.ShouldBe("+989121234567");
        contacts.Telegram.ShouldBe("pasargad_shop");
        contacts.Instagram.ShouldBeNull();
    }

    [Fact]
    public async Task Product_AShownOne_IsReadWithItsVariantsPhotosAndContacts()
    {
        var (admin, token) = await ShopAdminAsync();
        var category = await CreateCategoryAsync(admin, token, "مکمل");
        var product = await CreateProductAsync(
            admin, token, category.Id, "پروتئین وی", Variant("شکلاتی", 1_200_000m, 4), Variant("وانیلی", 2_100_000m, 0));
        var first = await AddPhotoAsync(admin, token, product.Id);
        var second = await AddPhotoAsync(admin, token, product.Id);

        using var anonymous = Fixture.CreateClient();
        var page = await ReadAsync<ShowcaseProductResponse>(anonymous, $"{ShowcaseProductsPath}/{product.Id}");

        page.Name.ShouldBe("پروتئین وی");
        page.Description.ShouldBe("توضیح کالا");
        page.CategoryName.ShouldBe("مکمل");
        page.Variants.Select(variant => (variant.Label, variant.Price, variant.InStock))
            .ShouldBe([("شکلاتی", 1_200_000m, true), ("وانیلی", 2_100_000m, false)]);
        page.Photos.Select(photo => photo.Id).ShouldBe([first.Id, second.Id]);
        page.Contacts.ShouldNotBeNull();
    }

    [Fact]
    public async Task Product_AHiddenOrUnknownOne_Returns404()
    {
        var (admin, token) = await ShopAdminAsync();
        var category = await CreateCategoryAsync(admin, token, "مکمل");
        var hidden = await CreateProductAsync(admin, token, category.Id, "کراتین", isVisible: false, Variant("", 900_000m, 3));

        using var anonymous = Fixture.CreateClient();
        using var hiddenPage = await anonymous.GetAsync($"{ShowcaseProductsPath}/{hidden.Id}", TestContext.Current.CancellationToken);
        using var unknown = await anonymous.GetAsync($"{ShowcaseProductsPath}/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);

        hiddenPage.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await hiddenPage.ReadErrorCodeAsync()).ShouldBe(ShopProductErrors.NotFound.Code);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Photo_AtTheSitesAddress_ServesTheWebpForAYear()
    {
        var (admin, token) = await ShopAdminAsync();
        var category = await CreateCategoryAsync(admin, token, "مکمل");
        var product = await CreateProductAsync(admin, token, category.Id, "کراتین", Variant("", 900_000m, 3));
        var photo = await AddPhotoAsync(admin, token, product.Id);

        using var anonymous = Fixture.CreateClient();
        using var small = await anonymous.GetAsync($"{ShowcasePhotosPath}/{photo.Id}/small", TestContext.Current.CancellationToken);
        using var large = await anonymous.GetAsync($"{ShowcasePhotosPath}/{photo.Id}/large", TestContext.Current.CancellationToken);
        using var unknown = await anonymous.GetAsync($"{ShowcasePhotosPath}/{Guid.CreateVersion7()}/small", TestContext.Current.CancellationToken);

        small.StatusCode.ShouldBe(HttpStatusCode.OK);
        small.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
        small.Headers.CacheControl!.ToString().ShouldContain("immutable");
        large.StatusCode.ShouldBe(HttpStatusCode.OK);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Photo_OfAHiddenProduct_IsNotOnTheSiteButStaysOnThePanel()
    {
        var (admin, token) = await ShopAdminAsync();
        var category = await CreateCategoryAsync(admin, token, "مکمل");
        var hidden = await CreateProductAsync(admin, token, category.Id, "کراتین", isVisible: false, Variant("", 900_000m, 3));
        var photo = await AddPhotoAsync(admin, token, hidden.Id);

        using var anonymous = Fixture.CreateClient();
        using var onTheSite = await anonymous.GetAsync($"{ShowcasePhotosPath}/{photo.Id}/large", TestContext.Current.CancellationToken);
        using var onThePanel = await anonymous.GetAsync($"/api/shop/photos/{photo.Id}/large", TestContext.Current.CancellationToken);

        // A hidden product is nowhere on the site (§16 *The showcase*); the shop admin's own list
        // still shows its photo, through the panel's address.
        onTheSite.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await onTheSite.ReadErrorCodeAsync()).ShouldBe(ShopProductErrors.PhotoNotFound.Code);
        onThePanel.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Showcase_WithAStaffASiteOrABrokenToken_AnswersAsWithout()
    {
        var (admin, staffToken) = await ShopAdminAsync();
        var category = await CreateCategoryAsync(admin, staffToken, "مکمل");
        await CreateProductAsync(admin, staffToken, category.Id, "کراتین", Variant("", 900_000m, 3));
        var siteToken = await SiteTokenAsync(admin);

        using var anonymous = Fixture.CreateClient();
        var expected = await anonymous.GetStringAsync(ShowcasePath, TestContext.Current.CancellationToken);

        // The showcase is the same for everyone: no token changes it, and none is refused.
        foreach (var bearer in new[] { staffToken, siteToken, "not-a-token" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, ShowcasePath).WithBearer(bearer);
            using var response = await anonymous.SendAsync(request, TestContext.Current.CancellationToken);

            response.StatusCode.ShouldBe(HttpStatusCode.OK, bearer);
            (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe(expected);
        }
    }

    [Fact]
    public async Task Showcase_ChangingAnything_IsNotThere()
    {
        var (admin, token) = await ShopAdminAsync();
        var category = await CreateCategoryAsync(admin, token, "مکمل");
        var product = await CreateProductAsync(admin, token, category.Id, "کراتین", Variant("", 900_000m, 3));

        // Even the shop admin's own token changes nothing at the site's address: the showcase only
        // reads, and a staff token is no one there (ADR 0010).
        using var put = await SendAsync(admin, token, HttpMethod.Put, $"{ShowcaseProductsPath}/{product.Id}", new { name = "دیگر" });
        using var post = await SendAsync(admin, token, HttpMethod.Post, ShowcaseProductsPath, new { name = "دیگر" });

        put.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        post.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var anonymous = Fixture.CreateClient();
        (await ReadAsync<ShowcaseResponse>(anonymous, ShowcasePath)).Products.ShouldHaveSingleItem().Name.ShouldBe("کراتین");
    }

    [Fact]
    public async Task Showcase_MoreRequestsThanTheLimitFromOneAddress_Returns429ForPhotosToo()
    {
        const int limit = 3;
        using var client = Fixture.CreateClient(new Dictionary<string, string?>
        {
            [$"{SiteShopRateLimitOptions.SectionName}:PermitLimit"] = limit.ToString(CultureInfo.InvariantCulture),
        });

        for (var attempt = 1; attempt <= limit; attempt++)
        {
            using var allowed = await client.GetAsync(ShowcasePath, TestContext.Current.CancellationToken);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK, $"request {attempt} is within the limit");
        }

        using var refused = await client.GetAsync(ShowcasePath, TestContext.Current.CancellationToken);
        using var photo = await client.GetAsync($"{ShowcasePhotosPath}/{Guid.CreateVersion7()}/small", TestContext.Current.CancellationToken);

        refused.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        (await refused.ReadErrorCodeAsync()).ShouldBe("General.TooManyRequests");
        photo.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private sealed record VariantInput(string Label, decimal Price, int Stock);

    private static VariantInput Variant(string label, decimal price, int stock) => new(label, price, stock);

    private async Task<(HttpClient Client, string Token)> ShopAdminAsync()
    {
        var client = Fixture.CreateClient();
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, "shop", Roles.Shop);
        return (client, await client.LoginForAccessTokenAsync("shop", TestUsers.Password));
    }

    /// <summary>A member's site token, signed in with the code as the app does (ADR 0010).</summary>
    private async Task<string> SiteTokenAsync(HttpClient client)
    {
        const string phone = "+989121234567";
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Members.Add(TestMembers.Seed("سارا محمدی", phone));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var signIn = await client.SignInWithCodeAsync(Fixture, phone);
        return (await signIn.ReadJsonAsync()).GetProperty("access").GetProperty("accessToken").GetString()!;
    }

    private static async Task<ShopCategoryResponse> CreateCategoryAsync(HttpClient client, string token, string name)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, CategoriesPath, new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ShopCategoryResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<ShopProductResponse> CreateProductAsync(
        HttpClient client, string token, Guid categoryId, string name, params VariantInput[] variants) =>
        CreateProductAsync(client, token, categoryId, name, isVisible: true, variants);

    private static async Task<ShopProductResponse> CreateProductAsync(
        HttpClient client, string token, Guid categoryId, string name, bool isVisible, params VariantInput[] variants)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, ProductsPath, new
        {
            name,
            description = "توضیح کالا",
            categoryId,
            isVisible,
            variants = variants
                .Select(variant => new { id = (Guid?)null, label = variant.Label, price = variant.Price, stock = variant.Stock })
                .ToArray(),
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ShopProductResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<ShopPhotoResponse> AddPhotoAsync(HttpClient client, string token, Guid productId)
    {
        var file = new ByteArrayContent(Jpeg(40, 30));
        file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        using var form = new MultipartFormDataContent { { file, "file", "photo.jpg" } };
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ProductsPath}/{productId}/photos") { Content = form };
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ShopProductResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().Photos[^1];
    }

    private static async Task<T> ReadAsync<T>(HttpClient client, string path)
        where T : class
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private static byte[] Jpeg(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SandyBrown);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }
}
