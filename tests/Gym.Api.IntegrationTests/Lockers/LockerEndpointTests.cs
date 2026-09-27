using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Paging;
using Gym.Application.Lockers;
using Gym.Domain.Lockers;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Persistence.Seed;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Lockers;

/// <summary>
/// <c>/api/lockers</c>. BUSINESS_RULES.md §6 and the permissions table in §1: the gym's 72 lockers
/// come with the migration and nobody creates one, while reading them and changing a locker's
/// service state belong to the front desk, which is where a broken locker is noticed.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class LockerEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string LockersPath = "/api/lockers";

    // ---- The fixed lockers ----

    [Fact]
    public void Migration_FreshDatabase_SeedsExactlyTheGymsLockersInService()
    {
        // Read by the fixture straight after migrating, before any reset put rows back: this is
        // the migration's own work, not the test harness's.
        var lockers = Fixture.LockersAfterMigration;

        lockers.Select(locker => locker.Number).ShouldBe(Enumerable.Range(1, Locker.Count));
        lockers.Select(locker => locker.Id).ShouldBe(LockerSeed.All.Select(seed => seed.Id));
        lockers.ShouldAllBe(locker => !locker.IsOutOfService);
    }

    [Fact]
    public async Task ListLockers_OneFullPage_Returns72LockersNumbered1To72()
    {
        var (client, _, staff) = await ClientsAsync();

        var page = await ListAsync(client, staff, $"?pageSize={PagingRules.MaxPageSize}");

        page.TotalCount.ShouldBe(Locker.Count);
        page.Items.Select(locker => locker.Number).ShouldBe(Enumerable.Range(1, Locker.Count));
        page.Items.ShouldAllBe(locker => !locker.IsOccupied && !locker.IsOutOfService);
    }

    [Fact]
    public async Task CreateLocker_EndpointRemoved_Returns405AndAddsNothing()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Post, LockersPath, new { number = 73 });

        // The path still answers GET, so a POST to it is "not allowed here" rather than "not found".
        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        (await CountLockersAsync()).ShouldBe(Locker.Count);
    }

    // ---- Permissions ----

    [Fact]
    public async Task GetLocker_AsStaff_Returns200()
    {
        var (client, _, staff) = await ClientsAsync();

        using var response = await SendAsync(client, staff, HttpMethod.Get, $"{LockersPath}/{TestLockers.IdOf(1)}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var locker = (await response.Content.ReadFromJsonAsync<LockerResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        locker.Number.ShouldBe(1);
    }

    [Fact]
    public async Task SetLockerOutOfService_AsStaff_Returns200AndTakesItOut()
    {
        var (client, _, staff) = await ClientsAsync();

        using var response = await PostAsync(client, staff, $"{LockersPath}/{TestLockers.IdOf(1)}/out-of-service");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(TestLockers.IdOf(1))).IsOutOfService.ShouldBeTrue();
    }

    [Fact]
    public async Task SetLockerInService_AsStaff_Returns200AndBringsItBack()
    {
        var (client, owner, staff) = await ClientsAsync();
        (await PostAsync(client, owner, $"{LockersPath}/{TestLockers.IdOf(1)}/out-of-service")).Dispose();

        using var response = await PostAsync(client, staff, $"{LockersPath}/{TestLockers.IdOf(1)}/in-service");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(TestLockers.IdOf(1))).IsOutOfService.ShouldBeFalse();
    }

    [Fact]
    public async Task ListLockers_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(LockersPath, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- List ----

    [Fact]
    public async Task ListLockers_SecondPage_ReturnsTheNextNumbersWithTheTotal()
    {
        var (client, owner, _) = await ClientsAsync();

        var page = await ListAsync(client, owner, "?page=2&pageSize=2");

        page.TotalCount.ShouldBe(Locker.Count);
        page.Items.Select(locker => locker.Number).ShouldBe([3, 4]);
    }

    [Fact]
    public async Task ListLockers_PageSizeOver100_Returns400()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{LockersPath}?pageSize=101", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---- Get ----

    [Fact]
    public async Task GetLocker_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await SendAsync(client, owner, HttpMethod.Get, $"{LockersPath}/{Guid.CreateVersion7()}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Lockers.NotFound");
    }

    // ---- Out of service / in service ----

    [Fact]
    public async Task SetLockerOutOfService_ThenInService_TogglesTheFlag()
    {
        var (client, owner, _) = await ClientsAsync();
        var id = TestLockers.IdOf(1);

        using var outOfService = await PostAsync(client, owner, $"{LockersPath}/{id}/out-of-service");
        outOfService.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(id)).IsOutOfService.ShouldBeTrue();

        using var inService = await PostAsync(client, owner, $"{LockersPath}/{id}/in-service");
        inService.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(id)).IsOutOfService.ShouldBeFalse();
    }

    [Fact]
    public async Task SetLockerOutOfService_AlreadyOutOfService_Returns200AndChangesNothing()
    {
        var (client, owner, _) = await ClientsAsync();
        var id = TestLockers.IdOf(1);
        using var first = await PostAsync(client, owner, $"{LockersPath}/{id}/out-of-service");
        var version = (await StoredAsync(id)).Version;

        using var second = await PostAsync(client, owner, $"{LockersPath}/{id}/out-of-service");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(id)).Version.ShouldBe(version, "nothing changed, so nothing was saved.");
    }

    [Fact]
    public async Task SetLockerOutOfService_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await PostAsync(client, owner, $"{LockersPath}/{Guid.CreateVersion7()}/out-of-service");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SetLockerInService_UnknownId_Returns404()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await PostAsync(client, owner, $"{LockersPath}/{Guid.CreateVersion7()}/in-service");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---- Database constraints ----

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Locker.Count + 1)]
    public async Task Lockers_NumberOutsideTheGymsInsertedDirectly_RejectedByACheckConstraint(int number)
    {
        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(InsertSql(Guid.CreateVersion7(), number)));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe(LockerConstraints.NumberRange);
    }

    [Fact]
    public async Task Lockers_DuplicateNumberInsertedDirectly_RejectedByTheUniqueIndex()
    {
        // Locker 5 is already there from the seed.
        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(InsertSql(Guid.CreateVersion7(), 5)));

        exception.SqlState.ShouldBe("23505");
        exception.ConstraintName.ShouldBe(LockerConstraints.UniqueNumber);
    }

    private static string InsertSql(Guid id, int number) =>
        $"""
        INSERT INTO lockers (id, number, is_out_of_service, created_at)
        VALUES ('{id}', {number}, false, now())
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

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string path) =>
        SendAsync(client, token, HttpMethod.Post, path, body: null);

    private static async Task<PagedResponse<LockerResponse>> ListAsync(HttpClient client, string token, string queryString)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, LockersPath + queryString, body: null);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<PagedResponse<LockerResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
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

    private async Task<Locker> StoredAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Lockers
            .AsNoTracking()
            .SingleAsync(locker => locker.Id == id, TestContext.Current.CancellationToken);
    }

    private async Task<int> CountLockersAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Lockers.CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
