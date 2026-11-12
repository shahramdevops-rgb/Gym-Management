using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Shop;
using Gym.Domain.Shop;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using SkiaSharp;

namespace Gym.Api.IntegrationTests.Shop;

/// <summary>
/// The online shop's categories, products, variants and photos (BUSINESS_RULES.md §16 <i>Products</i>,
/// task 16.3): the shop admin changes them, the Owner reads them, Staff never reach them; a photo is
/// re-encoded without its EXIF data and served to anyone.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ShopCatalogTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string CategoriesPath = "/api/shop/categories";
    private const string ProductsPath = "/api/shop/products";

    /// <summary>Written into the test photo's EXIF; must not survive the re-encoding.</summary>
    private const string ExifSecret = "GPS-SECRET-35.6892N-51.3890E";

    // ── Categories ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Categories_CreateRenameListAndDelete_WorkForTheShopAdmin()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);

        var created = await CreateCategoryAsync(client, token, "  مکمل  ");
        created.Name.ShouldBe("مکمل");
        created.ProductCount.ShouldBe(0);

        using var renamed = await SendAsync(client, token, HttpMethod.Put, $"{CategoriesPath}/{created.Id}",
            new { name = "مکمل‌ها", version = created.Version });
        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);

        var list = await GetAsync<List<ShopCategoryResponse>>(client, token, CategoriesPath);
        list.ShouldHaveSingleItem().Name.ShouldBe("مکمل‌ها");

        using var deleted = await SendAsync(client, token, HttpMethod.Delete, $"{CategoriesPath}/{created.Id}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetAsync<List<ShopCategoryResponse>>(client, token, CategoriesPath)).ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateCategory_ANameTakenOnceNormalized_Returns409()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        await CreateCategoryAsync(client, token, "پوشاک ي");

        using var response = await SendAsync(client, token, HttpMethod.Post, CategoriesPath, new { name = "پوشاک ی" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe(ShopCategoryErrors.NameAlreadyExists.Code);
    }

    [Fact]
    public async Task DeleteCategory_WithAProduct_Returns422AndKeepsIt()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var category = await CreateCategoryAsync(client, token, "مکمل");
        await CreateProductAsync(client, token, category.Id, "کراتین");

        using var response = await SendAsync(client, token, HttpMethod.Delete, $"{CategoriesPath}/{category.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(ShopCategoryErrors.NotEmpty.Code);
        (await GetAsync<List<ShopCategoryResponse>>(client, token, CategoriesPath)).ShouldHaveSingleItem().ProductCount.ShouldBe(1);
    }

    [Fact]
    public async Task Catalog_AsOwner_IsReadButNotChanged()
    {
        var (client, shopToken) = await ClientAsync("shop", Roles.Shop);
        var category = await CreateCategoryAsync(client, shopToken, "مکمل");
        var product = await CreateProductAsync(client, shopToken, category.Id, "کراتین");
        var ownerToken = await LoginAsync(client, "owner", Roles.Owner);

        using var categories = await SendAsync(client, ownerToken, HttpMethod.Get, CategoriesPath);
        using var products = await SendAsync(client, ownerToken, HttpMethod.Get, ProductsPath);
        using var one = await SendAsync(client, ownerToken, HttpMethod.Get, $"{ProductsPath}/{product.Id}");
        using var create = await SendAsync(client, ownerToken, HttpMethod.Post, CategoriesPath, new { name = "پوشاک" });
        using var upload = await UploadAsync(client, ownerToken, product.Id, Jpeg(40, 30));

        categories.StatusCode.ShouldBe(HttpStatusCode.OK);
        products.StatusCode.ShouldBe(HttpStatusCode.OK);
        one.StatusCode.ShouldBe(HttpStatusCode.OK);
        create.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        upload.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Catalog_AsStaff_IsRefused()
    {
        var (client, token) = await ClientAsync("desk", Roles.Staff);

        using var categories = await SendAsync(client, token, HttpMethod.Get, CategoriesPath);
        using var products = await SendAsync(client, token, HttpMethod.Get, ProductsPath);

        categories.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        products.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── Products and variants ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateProduct_WithVariants_IsReadBackAndListed()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var category = await CreateCategoryAsync(client, token, "مکمل");

        using var response = await SendAsync(client, token, HttpMethod.Post, ProductsPath, new
        {
            name = "پروتئین وی",
            description = "  ۲۴ گرم پروتئین در هر پیمانه ",
            categoryId = category.Id,
            isVisible = true,
            variants = new[]
            {
                new { id = (Guid?)null, label = "شکلاتی، ۱ کیلو", price = 1_200_000m, stock = 4 },
                new { id = (Guid?)null, label = "وانیلی، ۲ کیلو", price = 2_100_000m, stock = 0 },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var product = (await response.Content.ReadFromJsonAsync<ShopProductResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        product.Description.ShouldBe("۲۴ گرم پروتئین در هر پیمانه");
        product.Variants.Select(v => v.Label).ShouldBe(["شکلاتی، ۱ کیلو", "وانیلی، ۲ کیلو"]);

        var read = await GetAsync<ShopProductResponse>(client, token, $"{ProductsPath}/{product.Id}");
        read.Variants.Select(v => (v.Price, v.Stock)).ShouldBe([(1_200_000m, 4), (2_100_000m, 0)]);

        var item = (await GetAsync<List<ShopProductListItem>>(client, token, ProductsPath)).ShouldHaveSingleItem();
        item.CategoryName.ShouldBe("مکمل");
        item.MinPrice.ShouldBe(1_200_000m);
        item.MaxPrice.ShouldBe(2_100_000m);
        item.TotalStock.ShouldBe(4);
        item.VariantCount.ShouldBe(2);
        item.MainPhotoId.ShouldBeNull();
        item.IsVisible.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateProduct_APriceOfZero_Returns400UnderThatVariantsPrice()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var category = await CreateCategoryAsync(client, token, "مکمل");

        using var response = await SendAsync(client, token, HttpMethod.Post, ProductsPath, new
        {
            name = "کراتین",
            categoryId = category.Id,
            isVisible = true,
            variants = new[] { new { id = (Guid?)null, label = "", price = 0m, stock = 1 } },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var errors = body.RootElement.GetProperty("errors").EnumerateObject().ToList();
        var price = errors.ShouldHaveSingleItem();
        price.Name.ShouldContain("price", Case.Insensitive);
        price.Value[0].GetProperty("code").GetString().ShouldBe(ShopProductErrors.VariantPriceNotPositive.Code);
    }

    [Fact]
    public async Task CreateProduct_AnUnknownCategory_Returns404()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);

        using var response = await SendAsync(client, token, HttpMethod.Post, ProductsPath, ProductBody(Guid.CreateVersion7(), "کراتین"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe(ShopProductErrors.CategoryNotFound.Code);
    }

    [Fact]
    public async Task CreateProduct_ANameTaken_Returns409()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var category = await CreateCategoryAsync(client, token, "مکمل");
        await CreateProductAsync(client, token, category.Id, "کراتین");

        using var response = await SendAsync(client, token, HttpMethod.Post, ProductsPath, ProductBody(category.Id, " کراتین "));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe(ShopProductErrors.NameAlreadyExists.Code);
    }

    [Fact]
    public async Task UpdateProduct_KeepsAddsRemovesAndSwapsLabels()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var category = await CreateCategoryAsync(client, token, "مکمل");
        var product = await CreateProductAsync(client, token, category.Id, "پروتئین وی", "شکلاتی", "وانیلی", "موزی");
        var (chocolate, vanilla) = (product.Variants[0], product.Variants[1]);

        // The two kept variants swap labels in one save, which the deferred unique allows.
        using var response = await SendAsync(client, token, HttpMethod.Put, $"{ProductsPath}/{product.Id}", new
        {
            name = "پروتئین وی",
            description = (string?)null,
            categoryId = category.Id,
            isVisible = false,
            version = product.Version,
            variants = new[]
            {
                new { id = (Guid?)vanilla.Id, label = "شکلاتی", price = 1_300_000m, stock = 9 },
                new { id = (Guid?)chocolate.Id, label = "وانیلی", price = 1_250_000m, stock = 1 },
                new { id = (Guid?)null, label = "قهوه", price = 1_400_000m, stock = 2 },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var read = await GetAsync<ShopProductResponse>(client, token, $"{ProductsPath}/{product.Id}");
        read.IsVisible.ShouldBeFalse();
        read.Variants.Select(v => (v.Id == vanilla.Id, v.Label, v.Stock)).First().ShouldBe((true, "شکلاتی", 9));
        read.Variants.Select(v => v.Label).ShouldBe(["شکلاتی", "وانیلی", "قهوه"]);
        read.Variants.ShouldNotContain(v => v.Id == product.Variants[2].Id);
    }

    [Fact]
    public async Task UpdateProduct_OnAStaleVersion_Returns409()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var category = await CreateCategoryAsync(client, token, "مکمل");
        var product = await CreateProductAsync(client, token, category.Id, "کراتین");
        var body = new
        {
            name = "کراتین مونوهیدرات",
            categoryId = category.Id,
            isVisible = true,
            version = product.Version,
            variants = new[] { new { id = (Guid?)product.Variants[0].Id, label = "", price = 900_000m, stock = 3 } },
        };
        using (var first = await SendAsync(client, token, HttpMethod.Put, $"{ProductsPath}/{product.Id}", body))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var second = await SendAsync(client, token, HttpMethod.Put, $"{ProductsPath}/{product.Id}", body);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.ReadErrorCodeAsync()).ShouldBe(ShopProductErrors.ChangedConcurrently.Code);
    }

    // ── Photos ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddPhoto_APhoneJpegTurnedSideways_IsStoredUprightAsWebpWithoutItsExif()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var product = await ProductAsync(client, token);

        // 400 × 200 pixels whose EXIF says "turn a quarter clockwise", as a phone held upright saves it.
        using var response = await UploadAsync(client, token, product.Id, WithExif(Jpeg(400, 200), orientation: 6));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var photo = (await response.Content.ReadFromJsonAsync<ShopProductResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().Photos.ShouldHaveSingleItem();
        (photo.Width, photo.Height).ShouldBe((200, 400));

        var large = await File.ReadAllBytesAsync(FileOf(photo.Id, "large"), TestContext.Current.CancellationToken);
        Encoding.ASCII.GetString(large, 0, 4).ShouldBe("RIFF");
        Encoding.ASCII.GetString(large, 8, 4).ShouldBe("WEBP");
        Encoding.ASCII.GetString(large).ShouldNotContain(ExifSecret);
        Encoding.ASCII.GetString(large).ShouldNotContain("Exif");
        File.Exists(FileOf(photo.Id, "small")).ShouldBeTrue();
    }

    [Fact]
    public async Task AddPhoto_ALargePng_IsShrunkToFitBothSizes()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var product = await ProductAsync(client, token);

        using var response = await UploadAsync(client, token, product.Id, Png(2000, 1000), "photo.png", "image/png");

        var photo = (await response.Content.ReadFromJsonAsync<ShopProductResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().Photos.ShouldHaveSingleItem();
        (photo.Width, photo.Height).ShouldBe((1600, 800));
        using var small = SKBitmap.Decode(FileOf(photo.Id, "small"));
        (small.Width, small.Height).ShouldBe((480, 240));
    }

    [Fact]
    public async Task AddPhoto_NotAPhoto_Returns400AndWritesNothing()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var product = await ProductAsync(client, token);

        using var response = await UploadAsync(client, token, product.Id, Encoding.UTF8.GetBytes("%PDF-1.7 not a photo"), "x.jpg");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe("Photos.Unreadable");
        (await GetAsync<ShopProductResponse>(client, token, $"{ProductsPath}/{product.Id}")).Photos.ShouldBeEmpty();
    }

    [Fact]
    public async Task AddPhoto_ANinthPhoto_Returns422()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var product = await ProductAsync(client, token);
        for (var i = 0; i < ShopProduct.MaxPhotos; i++)
        {
            using var added = await UploadAsync(client, token, product.Id, Jpeg(40, 30));
            added.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var response = await UploadAsync(client, token, product.Id, Jpeg(40, 30));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(ShopProductErrors.TooManyPhotos.Code);
    }

    [Fact]
    public async Task GetPhoto_WithoutSigningIn_ServesTheWebpForAYear()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var product = await ProductAsync(client, token);
        var photo = await AddPhotoAsync(client, token, product.Id);

        using var anonymous = Fixture.CreateClient();
        using var large = await anonymous.GetAsync($"/api/shop/photos/{photo.Id}/large", TestContext.Current.CancellationToken);
        using var small = await anonymous.GetAsync($"/api/shop/photos/{photo.Id}/small", TestContext.Current.CancellationToken);
        using var otherSize = await anonymous.GetAsync($"/api/shop/photos/{photo.Id}/huge", TestContext.Current.CancellationToken);
        using var unknown = await anonymous.GetAsync($"/api/shop/photos/{Guid.CreateVersion7()}/large", TestContext.Current.CancellationToken);

        large.StatusCode.ShouldBe(HttpStatusCode.OK);
        large.Content.Headers.ContentType!.MediaType.ShouldBe("image/webp");
        large.Headers.CacheControl!.ToString().ShouldContain("immutable");
        small.StatusCode.ShouldBe(HttpStatusCode.OK);
        otherSize.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RemovePhoto_TheMainOne_DeletesItsFilesAndMakesTheNextMain()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var product = await ProductAsync(client, token);
        var first = await AddPhotoAsync(client, token, product.Id);
        var second = await AddPhotoAsync(client, token, product.Id);

        using var response = await SendAsync(client, token, HttpMethod.Delete, $"{ProductsPath}/{product.Id}/photos/{first.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        File.Exists(FileOf(first.Id, "large")).ShouldBeFalse();
        File.Exists(FileOf(first.Id, "small")).ShouldBeFalse();
        var item = (await GetAsync<List<ShopProductListItem>>(client, token, ProductsPath)).ShouldHaveSingleItem();
        item.MainPhotoId.ShouldBe(second.Id);
        using var gone = await Fixture.CreateClient().GetAsync($"/api/shop/photos/{first.Id}/large", TestContext.Current.CancellationToken);
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ReorderPhotos_PutsThemInTheNewOrder()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var product = await ProductAsync(client, token);
        var a = await AddPhotoAsync(client, token, product.Id);
        var b = await AddPhotoAsync(client, token, product.Id);
        var c = await AddPhotoAsync(client, token, product.Id);

        using var response = await SendAsync(client, token, HttpMethod.Put, $"{ProductsPath}/{product.Id}/photos/order",
            new { photoIds = new[] { c.Id, a.Id, b.Id } });
        using var invalid = await SendAsync(client, token, HttpMethod.Put, $"{ProductsPath}/{product.Id}/photos/order",
            new { photoIds = new[] { c.Id, a.Id } });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetAsync<ShopProductResponse>(client, token, $"{ProductsPath}/{product.Id}")).Photos.Select(p => p.Id).ShouldBe([c.Id, a.Id, b.Id]);
        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await invalid.ReadErrorCodeAsync()).ShouldBe(ShopProductErrors.PhotoOrderInvalid.Code);
    }

    // ── The database's own checks ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("stock = -1", "ck_shop_variants_stock_range")]
    [InlineData("price = 0", "ck_shop_variants_price_positive")]
    [InlineData("position = 20", "ck_shop_variants_position_range")]
    public async Task VariantsTable_AValueOutOfRange_IsRefusedByTheDatabase(string assignment, string constraint)
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var product = await ProductAsync(client, token);
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // The assignment comes from the test's own data, never from input.
#pragma warning disable EF1002
        var update = () => db.Database.ExecuteSqlRawAsync(
            $"UPDATE shop_variants SET {assignment} WHERE product_id = {{0}}", [product.Id], TestContext.Current.CancellationToken);
#pragma warning restore EF1002

        var refused = await update.ShouldThrowAsync<PostgresException>();
        refused.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        refused.ConstraintName.ShouldBe(constraint);
    }

    [Fact]
    public async Task PhotosTable_TwoPhotosOnOnePosition_AreRefusedAtTheCommit()
    {
        var (client, token) = await ClientAsync("shop", Roles.Shop);
        var product = await ProductAsync(client, token);
        await AddPhotoAsync(client, token, product.Id);
        await AddPhotoAsync(client, token, product.Id);
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var update = () => db.Database.ExecuteSqlAsync(
            $"UPDATE shop_photos SET position = 0 WHERE product_id = {product.Id}", TestContext.Current.CancellationToken);

        var refused = await update.ShouldThrowAsync<PostgresException>();
        refused.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        refused.ConstraintName.ShouldBe(ShopCatalogConstraints.UniquePhotoPosition);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────

    private string FileOf(Guid photoId, string size) => Path.Combine(Fixture.FilesRoot, ShopPhoto.KeyOf(photoId, size));

    private async Task<(HttpClient Client, string Token)> ClientAsync(string userName, string role)
    {
        var client = Fixture.CreateClient();
        return (client, await LoginAsync(client, userName, role));
    }

    private async Task<string> LoginAsync(HttpClient client, string userName, string role)
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName, role);
        return await client.LoginForAccessTokenAsync(userName, TestUsers.Password);
    }

    private static async Task<ShopCategoryResponse> CreateCategoryAsync(HttpClient client, string token, string name)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, CategoriesPath, new { name });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ShopCategoryResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static object ProductBody(Guid categoryId, string name, params string[] labels) => new
    {
        name,
        categoryId,
        isVisible = true,
        variants = (labels.Length == 0 ? [""] : labels)
            .Select(label => new { id = (Guid?)null, label, price = 1_000_000m, stock = 5 })
            .ToArray(),
    };

    private static async Task<ShopProductResponse> CreateProductAsync(
        HttpClient client, string token, Guid categoryId, string name, params string[] labels)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, ProductsPath, ProductBody(categoryId, name, labels));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ShopProductResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task<ShopProductResponse> ProductAsync(HttpClient client, string token)
    {
        var category = await CreateCategoryAsync(client, token, "مکمل");
        return await CreateProductAsync(client, token, category.Id, "کراتین");
    }

    private static async Task<ShopPhotoResponse> AddPhotoAsync(HttpClient client, string token, Guid productId)
    {
        using var response = await UploadAsync(client, token, productId, Jpeg(40, 30));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ShopProductResponse>(TestContext.Current.CancellationToken))
            .ShouldNotBeNull().Photos[^1];
    }

    private static Task<HttpResponseMessage> UploadAsync(
        HttpClient client, string token, Guid productId, byte[] bytes, string fileName = "photo.jpg", string contentType = "image/jpeg")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        var form = new MultipartFormDataContent { { file, "file", fileName } };
        var request = new HttpRequestMessage(HttpMethod.Post, $"{ProductsPath}/{productId}/photos") { Content = form };
        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string token, string path)
        where T : class
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, path);
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

    private static byte[] Jpeg(int width, int height) => Encode(width, height, SKEncodedImageFormat.Jpeg);

    private static byte[] Png(int width, int height) => Encode(width, height, SKEncodedImageFormat.Png);

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.SaddleBrown);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    /// <summary>
    /// Puts an EXIF segment right after the JPEG's start marker, as a phone does: the orientation and
    /// an image description standing in for the GPS position a real photo may carry.
    /// </summary>
    private static byte[] WithExif(byte[] jpeg, ushort orientation)
    {
        var text = Encoding.ASCII.GetBytes(ExifSecret + "\0");
        var tiff = new List<byte>();
        tiff.AddRange("MM"u8.ToArray());
        tiff.AddRange([0x00, 0x2A, 0x00, 0x00, 0x00, 0x08]);
        tiff.AddRange([0x00, 0x02]); // two entries, in tag order

        // ImageDescription (0x010E), ASCII, its text after the directory (8 + 2 + 24 + 4 = 38).
        tiff.AddRange([0x01, 0x0E, 0x00, 0x02]);
        tiff.AddRange(BigEndian((uint)text.Length));
        tiff.AddRange(BigEndian(38u));

        // Orientation (0x0112), SHORT, the value in the entry itself.
        tiff.AddRange([0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01]);
        tiff.AddRange([(byte)(orientation >> 8), (byte)orientation, 0x00, 0x00]);

        tiff.AddRange([0x00, 0x00, 0x00, 0x00]); // no next directory
        tiff.AddRange(text);

        var payload = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var length = payload.Length + 2;
        byte[] segment = [0xFF, 0xE1, (byte)(length >> 8), (byte)length, .. payload];

        return [.. jpeg[..2], .. segment, .. jpeg[2..]];
    }

    private static byte[] BigEndian(uint value) =>
        [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
