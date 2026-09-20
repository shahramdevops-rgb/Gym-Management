using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Paging;
using Gym.Application.Lockers;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Lockers;

/// <summary>
/// <c>/api/lockers</c>. BUSINESS_RULES.md §6 and the permissions table in §1: setting lockers up
/// (create, list, get, toggle service status) is the Owner's job; staff never reach it because
/// check-in (task 5.2) picks a free locker itself.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class LockerEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string LockersPath = "/api/lockers";

    // ---- Create ----

    [Fact]
    public async Task CreateLocker_AsOwner_Returns201WithTheValues()
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, 7);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var locker = (await response.Content.ReadFromJsonAsync<LockerResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{LockersPath}/{locker.Id}");
        locker.Number.ShouldBe(7);
        locker.IsOutOfService.ShouldBeFalse();
        locker.IsOccupied.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CreateLocker_NonPositiveNumber_Returns400WithFieldCode(int number)
    {
        var (client, owner, _) = await ClientsAsync();

        using var response = await CreateAsync(client, owner, number);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty("number")[0].GetProperty("code").GetString().ShouldBe("Lockers.NumberInvalid");
        (await CountLockersAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateLocker_DuplicateNumber_Returns409()
    {
        var (client, owner, _) = await ClientsAsync();
        await CreateLockerAsync(client, owner, 3);

        using var response = await CreateAsync(client, owner, 3);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Lockers.NumberAlreadyExists");
    }

    [Fact]
    public async Task CreateLocker_SameNumberInParallel_OneWinsAndTheRestGet409()
    {
        var (client, owner, _) = await ClientsAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CreateAsync(client, owner, 1)));

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
    public async Task CreateLocker_AsStaff_Returns403AndCreatesNothing()
    {
        var (client, _, staff) = await ClientsAsync();

        using var response = await CreateAsync(client, staff, 1);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).ShouldBe("Auth.Forbidden");
        (await CountLockersAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task ListLockers_AsStaff_Returns403()
    {
        var (client, owner, staff) = await ClientsAsync();
        await CreateLockerAsync(client, owner, 1);

        using var response = await SendAsync(client, staff, HttpMethod.Get, LockersPath, body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetLocker_AsStaff_Returns403()
    {
        var (client, owner, staff) = await ClientsAsync();
        var locker = await CreateLockerAsync(client, owner, 1);

        using var response = await SendAsync(client, staff, HttpMethod.Get, $"{LockersPath}/{locker.Id}", body: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("out-of-service")]
    [InlineData("in-service")]
    public async Task SetLockerServiceStatus_AsStaff_Returns403AndChangesNothing(string action)
    {
        var (client, owner, staff) = await ClientsAsync();
        var locker = await CreateLockerAsync(client, owner, 1);

        using var response = await PostAsync(client, staff, $"{LockersPath}/{locker.Id}/{action}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await StoredAsync(locker.Id)).IsOutOfService.ShouldBeFalse();
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
    public async Task ListLockers_Mixed_OrderedByNumberAscending()
    {
        var (client, owner, _) = await ClientsAsync();
        await CreateLockerAsync(client, owner, 3);
        await CreateLockerAsync(client, owner, 1);
        await CreateLockerAsync(client, owner, 2);

        var page = await ListAsync(client, owner, "");

        page.Items.Select(locker => locker.Number).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task ListLockers_NoAttendanceYet_EveryLockerShowsNotOccupied()
    {
        var (client, owner, _) = await ClientsAsync();
        await CreateLockerAsync(client, owner, 1);

        var page = await ListAsync(client, owner, "");

        page.Items.ShouldAllBe(locker => !locker.IsOccupied);
    }

    [Fact]
    public async Task ListLockers_SecondPage_ReturnsTheRestWithTheTotal()
    {
        var (client, owner, _) = await ClientsAsync();
        foreach (var number in new[] { 1, 2, 3 })
        {
            await CreateLockerAsync(client, owner, number);
        }

        var page = await ListAsync(client, owner, "?page=2&pageSize=2");

        page.TotalCount.ShouldBe(3);
        page.Items.ShouldHaveSingleItem().Number.ShouldBe(3);
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
        var locker = await CreateLockerAsync(client, owner, 1);

        using var outOfService = await PostAsync(client, owner, $"{LockersPath}/{locker.Id}/out-of-service");
        outOfService.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(locker.Id)).IsOutOfService.ShouldBeTrue();

        using var inService = await PostAsync(client, owner, $"{LockersPath}/{locker.Id}/in-service");
        inService.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(locker.Id)).IsOutOfService.ShouldBeFalse();
    }

    [Fact]
    public async Task SetLockerOutOfService_AlreadyOutOfService_Returns200AndChangesNothing()
    {
        var (client, owner, _) = await ClientsAsync();
        var locker = await CreateLockerAsync(client, owner, 1);
        using var first = await PostAsync(client, owner, $"{LockersPath}/{locker.Id}/out-of-service");
        var version = (await StoredAsync(locker.Id)).Version;

        using var second = await PostAsync(client, owner, $"{LockersPath}/{locker.Id}/out-of-service");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await StoredAsync(locker.Id)).Version.ShouldBe(version, "nothing changed, so nothing was saved.");
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
    public async Task Lockers_NonPositiveNumberInsertedDirectly_RejectedByACheckConstraint(int number)
    {
        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(InsertSql(Guid.CreateVersion7(), number)));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe("ck_lockers_number_positive");
    }

    [Fact]
    public async Task Lockers_DuplicateNumberInsertedDirectly_RejectedByTheUniqueIndex()
    {
        await ExecuteSqlAsync(InsertSql(Guid.CreateVersion7(), 5));

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

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string token, int number) =>
        SendAsync(client, token, HttpMethod.Post, LockersPath, new { number });

    private static async Task<LockerResponse> CreateLockerAsync(HttpClient client, string token, int number)
    {
        using var response = await CreateAsync(client, token, number);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LockerResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
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

    private async Task<Gym.Domain.Lockers.Locker> StoredAsync(Guid id)
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
