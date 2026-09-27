using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Pricing;
using Gym.Domain.Audit;
using Gym.Domain.Pricing;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Pricing;

/// <summary>
/// The gym's two prices: <c>GET</c> and <c>PUT /api/pricing</c> (BUSINESS_RULES.md §3 <i>Prices</i>,
/// task 6.5.6). The Owner sets them; everyone sells at them.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class PricingEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Path = "/api/pricing";

    // ---- The seeded row ----

    [Fact]
    public void Migration_SeedsOnePriceListWithBothPricesEmpty()
    {
        // Read right after migrating, before any reset: the migration's own row, not the fixture's copy.
        var row = Fixture.PriceListsAfterMigration.ShouldHaveSingleItem();

        row.Id.ShouldBe(PriceList.TheId);
        row.SessionPrice.ShouldBeNull();
        row.SingleVisitPrice.ShouldBeNull();
    }

    [Fact]
    public async Task Database_SecondPriceList_IsRefused()
    {
        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteAsync($"INSERT INTO price_lists (id, created_at) VALUES ('{Guid.CreateVersion7()}', now())"));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("ck_price_lists_single_row");
    }

    [Fact]
    public async Task Database_NegativePrice_IsRefused()
    {
        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteAsync("UPDATE price_lists SET session_price = -1"));

        exception.ConstraintName.ShouldBe("ck_price_lists_prices_not_negative");
    }

    // ---- Get ----

    [Fact]
    public async Task Get_BeforeTheOwnerSetsThem_ReturnsBothEmpty()
    {
        var (client, token) = await StaffClientAsync();

        var prices = await GetOkAsync(client, token);

        prices.SessionPrice.ShouldBeNull();
        prices.SingleVisitPrice.ShouldBeNull();
    }

    [Fact]
    public async Task Get_AsStaff_SeesThePricesTheDeskSellsAt()
    {
        var (client, token) = await StaffClientAsync();
        await TestPlans.SetPricesAsync(Fixture, sessionPrice: 100_000m, singleVisitPrice: 150_000m);

        var prices = await GetOkAsync(client, token);

        prices.SessionPrice.ShouldBe(100_000m);
        prices.SingleVisitPrice.ShouldBe(150_000m);
    }

    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Update ----

    [Fact]
    public async Task Update_AsOwner_SetsBothPrices()
    {
        var (client, token, _) = await OwnerClientAsync();
        var before = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, 100_000m, 150_000m, before.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await ReadAsync(response);
        updated.SessionPrice.ShouldBe(100_000m);
        updated.SingleVisitPrice.ShouldBe(150_000m);
        updated.Version.ShouldNotBe(before.Version);

        var reread = await GetOkAsync(client, token);
        reread.SessionPrice.ShouldBe(100_000m);
        reread.SingleVisitPrice.ShouldBe(150_000m);
    }

    [Fact]
    public async Task Update_AsOwner_WritesTheOldAndNewPriceToTheAuditLog()
    {
        var (client, token, ownerId) = await OwnerClientAsync();
        var first = await ReadAsync(await UpdateAsync(client, token, 100_000m, 150_000m, (await GetOkAsync(client, token)).Version));

        (await UpdateAsync(client, token, 120_000m, 150_000m, first.Version)).EnsureSuccessStatusCode().Dispose();

        await using var scope = Fixture.CreateScope();
        var update = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .Where(log => log.EntityType == nameof(PriceList) && log.Action == AuditAction.Update)
            .OrderByDescending(log => log.OccurredAt)
            .FirstAsync(TestContext.Current.CancellationToken);
        update.UserId.ShouldBe(ownerId);
        Value(update.OldValues, nameof(PriceList.SessionPrice)).GetDecimal().ShouldBe(100_000m);
        Value(update.NewValues, nameof(PriceList.SessionPrice)).GetDecimal().ShouldBe(120_000m);
    }

    [Fact]
    public async Task Update_AsStaff_Returns403AndChangesNothing()
    {
        var (staff, staffToken) = await StaffClientAsync();
        var before = await GetOkAsync(staff, staffToken);

        using var response = await UpdateAsync(staff, staffToken, 100_000m, 150_000m, before.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await GetOkAsync(staff, staffToken)).SessionPrice.ShouldBeNull();
    }

    [Fact]
    public async Task Update_StaleVersion_Returns409ChangedConcurrently()
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);
        (await UpdateAsync(client, token, 100_000m, 150_000m, read.Version)).EnsureSuccessStatusCode().Dispose();

        using var response = await UpdateAsync(client, token, 90_000m, 150_000m, read.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Pricing.ChangedConcurrently");
        (await GetOkAsync(client, token)).SessionPrice.ShouldBe(100_000m);
    }

    [Theory]
    [InlineData(-1, 150_000, "sessionPrice", "Pricing.PriceNegative")]
    [InlineData(100_000.005, 150_000, "sessionPrice", "Pricing.PriceTooManyDecimals")]
    [InlineData(100_000, -1, "singleVisitPrice", "Pricing.PriceNegative")]
    public async Task Update_InvalidPrice_Returns400WithFieldCode(
        double sessionPrice, double singleVisitPrice, string field, string code)
    {
        var (client, token, _) = await OwnerClientAsync();
        var read = await GetOkAsync(client, token);

        using var response = await UpdateAsync(client, token, (decimal)sessionPrice, (decimal)singleVisitPrice, read.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString().ShouldBe(code);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private async Task<(HttpClient Client, string Token, Guid UserId)> OwnerClientAsync()
    {
        var owner = await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password), owner.Id);
    }

    private static async Task<PricesResponse> GetOkAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Path);
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await ReadAsync(response);
    }

    private static Task<HttpResponseMessage> UpdateAsync(
        HttpClient client, string token, decimal sessionPrice, decimal singleVisitPrice, uint version)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, Path)
        {
            Content = JsonContent.Create(new { sessionPrice, singleVisitPrice, version }),
        };

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private static async Task<PricesResponse> ReadAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<PricesResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

    private static JsonElement Value(string? json, string property)
    {
        using var document = JsonDocument.Parse(json.ShouldNotBeNull());

        return document.RootElement.GetProperty(property).Clone();
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
