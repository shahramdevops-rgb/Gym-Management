using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Members;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Members;

/// <summary>
/// <c>POST /api/members</c> and <c>PUT /api/members/{id}</c>. The rules under test are
/// BUSINESS_RULES.md §2.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class MemberEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string MembersPath = "/api/members";

    [Fact]
    public async Task CreateMember_AsStaff_Returns201WithTheNormalizedPhone()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SendAsync(client, token, HttpMethod.Post, MembersPath,
            new { fullName = "  رضا احمدی ", phoneNumber = "0912 123 4567", notes = "عضو قدیمی" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var member = (await response.Content.ReadFromJsonAsync<MemberResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"{MembersPath}/{member.Id}");
        member.FullName.ShouldBe("رضا احمدی");
        member.PhoneNumber.ShouldBe("+989121234567");
        member.Notes.ShouldBe("عضو قدیمی");
        member.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("۰۹۱۲۱۲۳۴۵۶۷")]
    [InlineData("+98 912 123 4567")]
    [InlineData("0098-912-123-4567")]
    [InlineData("9121234567")]
    public async Task CreateMember_SamePhoneInAnotherFormat_Returns409PhoneAlreadyExists(string sameNumber)
    {
        var (client, token) = await StaffClientAsync();
        using var first = await CreateAsync(client, token, "رضا", "09121234567");
        first.StatusCode.ShouldBe(HttpStatusCode.Created);

        using var second = await CreateAsync(client, token, "علی", sameNumber);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.ReadErrorCodeAsync()).ShouldBe("Members.PhoneAlreadyExists");
        (await CountMembersAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task CreateMember_SamePhoneInParallel_OneWinsAndTheRestGet409()
    {
        var (client, token) = await StaffClientAsync();

        // Several requests can pass the "does this phone exist" check at the same moment; only
        // the unique index can decide, and the losers must hear 409, not 500.
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            CreateAsync(client, token, $"عضو {i}", i % 2 == 0 ? "09121234567" : "۰۹۱۲ ۱۲۳ ۴۵۶۷")));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            responses.Count(response => response.StatusCode == HttpStatusCode.Conflict).ShouldBe(5);
            (await CountMembersAsync()).ShouldBe(1);
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
    public async Task CreateMember_PhoneOfAnInactiveMember_IsStillADuplicate()
    {
        var (client, token) = await StaffClientAsync();
        using var first = await CreateAsync(client, token, "رضا", "09121234567");
        await ExecuteSqlAsync("UPDATE members SET is_active = false");

        using var second = await CreateAsync(client, token, "علی", "09121234567");

        (await second.ReadErrorCodeAsync()).ShouldBe("Members.PhoneAlreadyExists");
    }

    [Theory]
    [InlineData("02188776655", "Members.PhoneNotMobile")]
    [InlineData("+447911123456", "Members.PhoneNotIranian")]
    [InlineData("12345", "Members.PhoneInvalid")]
    public async Task CreateMember_UnacceptablePhone_Returns400WithTheReason(string phone, string expectedCode)
    {
        var (client, token) = await StaffClientAsync();

        using var response = await CreateAsync(client, token, "رضا", phone);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe(expectedCode);
    }

    [Theory]
    [InlineData("   ", "09121234567", "fullName", "Members.FullNameRequired")]
    [InlineData("رضا", "", "phoneNumber", "Members.PhoneRequired")]
    public async Task CreateMember_MissingField_Returns400WithFieldCode(string fullName, string phone, string field, string expectedCode)
    {
        var (client, token) = await StaffClientAsync();

        using var response = await CreateAsync(client, token, fullName, phone);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        body.RootElement.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString().ShouldBe(expectedCode);
    }

    [Fact]
    public async Task CreateMember_Stamped_WithTheUserWhoCreatedIt()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await CreateAsync(client, token, "رضا", "09121234567");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var staffId = await db.Users.Where(user => user.UserName == "staff").Select(user => user.Id).SingleAsync(TestContext.Current.CancellationToken);
        (await db.Members.SingleAsync(TestContext.Current.CancellationToken)).CreatedBy.ShouldBe(staffId);
    }

    [Fact]
    public async Task UpdateMember_ValidChange_Returns200AndKeepsTheSearchColumnInStep()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");

        using var response = await UpdateAsync(client, token, member.Id, $"{(char)0x0643}ر{(char)0x064A}م", "09351234567", "تغییر شماره", member.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = (await response.Content.ReadFromJsonAsync<MemberResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        updated.PhoneNumber.ShouldBe("+989351234567");
        updated.Version.ShouldNotBe(member.Version, "every save gives the member a new version.");

        await using var scope = Fixture.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Members.SingleAsync(TestContext.Current.CancellationToken);
        stored.NormalizedFullName.ShouldBe("کریم");
    }

    [Fact]
    public async Task UpdateMember_KeepingItsOwnPhone_Succeeds()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");

        using var response = await UpdateAsync(client, token, member.Id, "رضا احمدی", "۰۹۱۲۱۲۳۴۵۶۷", null, member.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateMember_TakingAnotherMembersPhone_Returns409()
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا", "09121234567");
        var ali = await CreateMemberAsync(client, token, "علی", "09351234567");

        using var response = await UpdateAsync(client, token, ali.Id, "علی", "+98 912 123 4567", null, ali.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.PhoneAlreadyExists");
    }

    [Fact]
    public async Task UpdateMember_StaleVersion_Returns409AndChangesNothing()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");

        // Someone else saves first.
        using var first = await UpdateAsync(client, token, member.Id, "رضا احمدی", "09121234567", null, member.Version);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);

        // This edit was made on the version read before that save.
        using var stale = await UpdateAsync(client, token, member.Id, "نام دیگر", "09121234567", null, member.Version);

        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await stale.ReadErrorCodeAsync()).ShouldBe("Members.ChangedConcurrently");

        await using var scope = Fixture.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().Members.SingleAsync(TestContext.Current.CancellationToken))
            .FullName.ShouldBe("رضا احمدی");
    }

    [Fact]
    public async Task UpdateMember_InactiveMember_CanStillBeEdited()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        await ExecuteSqlAsync("UPDATE members SET is_active = false");
        var version = await CurrentVersionAsync(member.Id);

        using var response = await UpdateAsync(client, token, member.Id, "رضا", "09351234567", null, version);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateMember_UnknownId_Returns404()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await UpdateAsync(client, token, Guid.CreateVersion7(), "رضا", "09121234567", null, 0);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.NotFound");
    }

    [Fact]
    public async Task CreateMember_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.PostAsJsonAsync(
            MembersPath, new { fullName = "رضا", phoneNumber = "09121234567" }, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Members_DuplicatePhoneInsertedDirectly_RejectedByTheUniqueIndex()
    {
        await ExecuteSqlAsync(InsertSql("+989121234567"));

        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(InsertSql("+989121234567")));

        exception.SqlState.ShouldBe("23505");
        exception.ConstraintName.ShouldBe(MemberConstraints.UniquePhone);
    }

    [Fact]
    public async Task Members_UnnormalizedPhoneInsertedDirectly_RejectedByTheCheckConstraint()
    {
        // "0912..." and "+98912..." would otherwise be two different strings to the unique index.
        var exception = await Should.ThrowAsync<PostgresException>(() => ExecuteSqlAsync(InsertSql("09121234567")));

        exception.SqlState.ShouldBe("23514");
        exception.ConstraintName.ShouldBe("ck_members_phone_number_e164");
    }

    private static string InsertSql(string phone) =>
        $"""
        INSERT INTO members (id, full_name, normalized_full_name, phone_number, is_active, created_at)
        VALUES ('{Guid.CreateVersion7()}', 'x', 'x', '{phone}', true, now())
        """;

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private static Task<HttpResponseMessage> CreateAsync(HttpClient client, string token, string fullName, string phoneNumber) =>
        SendAsync(client, token, HttpMethod.Post, MembersPath, new { fullName, phoneNumber });

    private static async Task<MemberResponse> CreateMemberAsync(HttpClient client, string token, string fullName, string phoneNumber)
    {
        using var response = await CreateAsync(client, token, fullName, phoneNumber);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MemberResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> UpdateAsync(
        HttpClient client, string token, Guid id, string fullName, string phoneNumber, string? notes, uint version) =>
        SendAsync(client, token, HttpMethod.Put, $"{MembersPath}/{id}", new { fullName, phoneNumber, notes, version });

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path, object body)
    {
        var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) };

        return client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private async Task<int> CountMembersAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Members.CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task<uint> CurrentVersionAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Members
            .Where(member => member.Id == id)
            .Select(member => member.Version)
            .SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
