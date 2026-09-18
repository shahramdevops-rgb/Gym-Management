using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Staff;
using Gym.Domain.Audit;
using Gym.Domain.Auth;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Audit;

/// <summary>
/// The audit log, driven through the real endpoints where possible so the rows are the ones
/// production would write. The rules under test are BUSINESS_RULES.md §11.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class AuditLogTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string TemporaryPassword = "Temp1234";

    [Fact]
    public async Task Update_DeactivatingStaff_WritesOldAndNewValuesOfTheChangedPropertyOnly()
    {
        var (client, ownerToken, ownerId) = await OwnerClientAsync();
        var staff = await CreateStaffAsync(client, ownerToken, "reza");

        using var deactivate = await SendAsync(client, ownerToken, HttpMethod.Post, $"/api/staff/{staff.Id}/deactivate");
        deactivate.EnsureSuccessStatusCode();

        var update = (await LoadAuditAsync())
            .Single(log => log.EntityType == nameof(User) && log.EntityId == staff.Id.ToString() && log.Action == AuditAction.Update);

        update.UserId.ShouldBe(ownerId, "the Owner made the change.");
        // Changed properties only: not the whole user, and not the concurrency stamp that
        // changes on every save.
        Properties(update.OldValues).ShouldBe([nameof(User.IsActive)]);
        Properties(update.NewValues).ShouldBe([nameof(User.IsActive)]);
        Value(update.OldValues, "IsActive").GetBoolean().ShouldBeTrue();
        Value(update.NewValues, "IsActive").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Insert_CreatingStaff_WritesNewValuesAndNoOldValues()
    {
        var (client, ownerToken, ownerId) = await OwnerClientAsync();

        var staff = await CreateStaffAsync(client, ownerToken, "reza");

        var insert = (await LoadAuditAsync())
            .Single(log => log.EntityType == nameof(User) && log.EntityId == staff.Id.ToString() && log.Action == AuditAction.Insert);
        insert.UserId.ShouldBe(ownerId);
        insert.OldValues.ShouldBeNull();
        Value(insert.NewValues, nameof(User.UserName)).GetString().ShouldBe("reza");
        Value(insert.NewValues, nameof(User.MustChangePassword)).GetBoolean().ShouldBeTrue();

        // Adding the role is its own row, keyed by both halves of the composite key.
        (await LoadAuditAsync()).ShouldContain(log =>
            log.EntityType == "IdentityUserRole<Guid>" &&
            log.Action == AuditAction.Insert &&
            log.EntityId.StartsWith(staff.Id.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task Delete_RemovingARole_WritesOldValuesAndNoNewValues()
    {
        var user = await TestUsers.CreateAsync(Fixture);

        await using (var scope = Fixture.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var tracked = await userManager.FindByIdAsync(user.Id.ToString());
            (await userManager.RemoveFromRoleAsync(tracked!, Roles.Staff)).Succeeded.ShouldBeTrue();
        }

        var delete = (await LoadAuditAsync()).Single(log => log.Action == AuditAction.Delete);
        delete.EntityType.ShouldBe("IdentityUserRole<Guid>");
        delete.NewValues.ShouldBeNull();
        Value(delete.OldValues, "UserId").GetGuid().ShouldBe(user.Id);
    }

    [Fact]
    public async Task PasswordFlows_NeverWriteSecretsToTheAuditLog()
    {
        var (client, ownerToken, _) = await OwnerClientAsync();
        var staff = await CreateStaffAsync(client, ownerToken, "reza");
        using var reset = await SendAsync(
            client, ownerToken, HttpMethod.Post, $"/api/staff/{staff.Id}/reset-password", new { temporaryPassword = "Fresh5678" });
        reset.EnsureSuccessStatusCode();
        using var login = await client.LoginAsync("reza", "Fresh5678");
        using var refresh = await client.RefreshAsync(login.ReadRefreshToken());

        var secrets = await LoadSecretsAsync();
        secrets.ShouldNotBeEmpty("the test proves nothing unless there are secrets to look for.");

        var logs = await LoadAuditAsync();
        logs.ShouldNotBeEmpty();
        foreach (var log in logs)
        {
            var json = $"{log.OldValues} {log.NewValues}";

            // Neither the property names nor the values themselves, in any row.
            json.ShouldNotContain("PasswordHash");
            json.ShouldNotContain("SecurityStamp");
            json.ShouldNotContain("TokenHash");
            foreach (var secret in secrets)
            {
                json.ShouldNotContain(secret);
            }
        }
    }

    [Fact]
    public async Task Update_ThatChangesOnlyExcludedProperties_WritesNoRow()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        var before = (await LoadAuditAsync()).Count;

        await using (var scope = Fixture.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
            var tracked = await userManager.FindByIdAsync(user.Id.ToString());

            // Changes SecurityStamp and ConcurrencyStamp, both excluded.
            (await userManager.UpdateSecurityStampAsync(tracked!)).Succeeded.ShouldBeTrue();
        }

        (await LoadAuditAsync()).Count.ShouldBe(before);
    }

    [Fact]
    public async Task FailedSave_RollsBackItsAuditRowsToo()
    {
        var user = await TestUsers.CreateAsync(Fixture);
        await SaveTokenAsync(RefreshToken.Issue(user.Id, "same-hash", DateTimeOffset.UtcNow));
        var before = (await LoadAuditAsync()).Count;

        await Should.ThrowAsync<DbUpdateException>(
            () => SaveTokenAsync(RefreshToken.Issue(user.Id, "same-hash", DateTimeOffset.UtcNow)));

        // Same save, same transaction: the log never describes a change that did not happen.
        (await LoadAuditAsync()).Count.ShouldBe(before);
    }

    [Theory]
    [InlineData("UPDATE audit_logs SET entity_id = 'rewritten'")]
    [InlineData("DELETE FROM audit_logs")]
    public async Task AuditLogs_UpdateOrDelete_RejectedByTheDatabase(string sql)
    {
        await TestUsers.CreateAsync(Fixture);
        (await LoadAuditAsync()).ShouldNotBeEmpty();

        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        var exception = await Should.ThrowAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));

        exception.MessageText.ShouldContain("append-only");
    }

    [Fact]
    public async Task Reset_BetweenTests_EmptiesTheAuditLogDespiteTheTrigger()
    {
        await TestUsers.CreateAsync(Fixture);
        (await LoadAuditAsync()).ShouldNotBeEmpty();

        // Respawn truncates, and row-level triggers do not fire for TRUNCATE. If it ever
        // switched to DELETE, every test after the first audited one would inherit rows.
        await Fixture.ResetAsync();

        (await LoadAuditAsync()).ShouldBeEmpty();
    }

    private async Task<(HttpClient Client, string OwnerToken, Guid OwnerId)> OwnerClientAsync()
    {
        var owner = await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password), owner.Id);
    }

    private static async Task<StaffResponse> CreateStaffAsync(HttpClient client, string ownerToken, string userName)
    {
        using var response = await SendAsync(
            client, ownerToken, HttpMethod.Post, "/api/staff",
            new { userName, fullName = "کارمند", temporaryPassword = TemporaryPassword });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<StaffResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string accessToken, HttpMethod method, string path, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return client.SendAsync(request.WithBearer(accessToken), TestContext.Current.CancellationToken);
    }

    private async Task<List<AuditLog>> LoadAuditAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Every password hash, security stamp and token hash currently in the database.</summary>
    private async Task<List<string>> LoadSecretsAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var userSecrets = await db.Users
            .Select(user => new[] { user.PasswordHash, user.SecurityStamp })
            .ToListAsync(TestContext.Current.CancellationToken);
        var tokenHashes = await db.RefreshTokens.Select(token => token.TokenHash).ToListAsync(TestContext.Current.CancellationToken);

        return [.. userSecrets.SelectMany(pair => pair).OfType<string>(), .. tokenHashes];
    }

    private async Task SaveTokenAsync(RefreshToken token)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.RefreshTokens.Add(token);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static IEnumerable<string> Properties(string? json)
    {
        using var document = JsonDocument.Parse(json.ShouldNotBeNull());

        return [.. document.RootElement.EnumerateObject().Select(property => property.Name)];
    }

    private static JsonElement Value(string? json, string property)
    {
        using var document = JsonDocument.Parse(json.ShouldNotBeNull());

        return document.RootElement.GetProperty(property).Clone();
    }
}
