using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Paging;
using Gym.Application.Members;
using Gym.Domain.Audit;
using Gym.Domain.Members;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Members;

/// <summary>
/// <c>GET /api/members</c>, <c>GET /api/members/{id}</c>, and deactivate / reactivate. The rules
/// under test are BUSINESS_RULES.md §2 (search) and §13 (Persian text normalization).
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class MemberQueryTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string MembersPath = "/api/members";

    // Look-alike and invisible characters as code points: on screen they match their Persian twins.
    private const char ArabicYe = (char)0x064A;
    private const char ArabicKaf = (char)0x0643;
    private const char HalfSpace = (char)0x200C;

    // ---- Get by id ----

    [Fact]
    public async Task GetMember_ExistingId_Returns200WithTheMember()
    {
        var (client, token) = await StaffClientAsync();
        var created = await CreateMemberAsync(client, token, "رضا احمدی", "09121234567");

        using var response = await SendAsync(client, token, HttpMethod.Get, $"{MembersPath}/{created.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var member = (await response.Content.ReadFromJsonAsync<MemberResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();

        // Postgres keeps microseconds, .NET ticks are 100 ns: the create response (built in memory)
        // can carry a digit the stored value lost. Every other field must match exactly.
        member.CreatedAt.ShouldBe(created.CreatedAt, TimeSpan.FromMicroseconds(1));
        member.ShouldBe(created with { CreatedAt = member.CreatedAt });
    }

    [Fact]
    public async Task GetMember_UnknownId_Returns404NotFound()
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SendAsync(client, token, HttpMethod.Get, $"{MembersPath}/{Guid.CreateVersion7()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.NotFound");
    }

    [Fact]
    public async Task GetMember_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync($"{MembersPath}/{Guid.CreateVersion7()}", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Deactivate and reactivate ----

    [Fact]
    public async Task DeactivateMember_ActiveMember_Returns200AndIsInactive()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");

        using var response = await SendAsync(client, token, HttpMethod.Post, $"{MembersPath}/{member.Id}/deactivate");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<MemberResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull().IsActive.ShouldBeFalse();
        (await LoadMemberAsync(member.Id)).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task ReactivateMember_InactiveMember_Returns200AndIsActive()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        using var deactivated = await SendAsync(client, token, HttpMethod.Post, $"{MembersPath}/{member.Id}/deactivate");

        using var response = await SendAsync(client, token, HttpMethod.Post, $"{MembersPath}/{member.Id}/reactivate");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoadMemberAsync(member.Id)).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task DeactivateMember_AlreadyInactive_Returns200AndChangesNothing()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        using var first = await SendAsync(client, token, HttpMethod.Post, $"{MembersPath}/{member.Id}/deactivate");
        var versionAfterFirst = (await LoadMemberAsync(member.Id)).Version;

        using var second = await SendAsync(client, token, HttpMethod.Post, $"{MembersPath}/{member.Id}/deactivate");

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoadMemberAsync(member.Id)).Version.ShouldBe(versionAfterFirst, "nothing changed, so nothing was saved.");
    }

    [Fact]
    public async Task ReactivateMember_AlreadyActive_Returns200()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");

        using var response = await SendAsync(client, token, HttpMethod.Post, $"{MembersPath}/{member.Id}/reactivate");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LoadMemberAsync(member.Id)).IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("deactivate")]
    [InlineData("reactivate")]
    public async Task SetMemberActive_UnknownId_Returns404NotFound(string action)
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SendAsync(client, token, HttpMethod.Post, $"{MembersPath}/{Guid.CreateVersion7()}/{action}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Members.NotFound");
    }

    [Fact]
    public async Task DeactivateMember_Audited_WithTheOldAndNewStatus()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");

        using var response = await SendAsync(client, token, HttpMethod.Post, $"{MembersPath}/{member.Id}/deactivate");

        await using var scope = Fixture.CreateScope();
        var audit = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs
            .AsNoTracking()
            .Where(log => log.EntityId == member.Id.ToString() && log.Action == AuditAction.Update)
            .SingleAsync(TestContext.Current.CancellationToken);
        audit.OldValues.ShouldNotBeNull().ShouldContain("true");
        audit.NewValues.ShouldNotBeNull().ShouldContain("false");
    }

    // ---- Paged list ----

    [Fact]
    public async Task ListMembers_SeveralPages_ReturnsEachMemberOnceInNameOrder()
    {
        var (client, token) = await StaffClientAsync();
        string[] names = ["هادی", "بهرام", "الهام", "مریم", "رضا"];
        for (var i = 0; i < names.Length; i++)
        {
            await CreateMemberAsync(client, token, names[i], $"0912123456{i}");
        }

        var page1 = await ListAsync(client, token, "?page=1&pageSize=2");
        var page2 = await ListAsync(client, token, "?page=2&pageSize=2");
        var page3 = await ListAsync(client, token, "?page=3&pageSize=2");

        page1.TotalCount.ShouldBe(5);
        page1.Items.Count.ShouldBe(2);
        page3.Items.Count.ShouldBe(1);
        page2.Page.ShouldBe(2);
        page2.PageSize.ShouldBe(2);

        var all = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(member => member.FullName).ToList();
        all.ShouldBe(names.Order(StringComparer.Ordinal).ToList());
    }

    [Theory]
    [InlineData("?page=0", "Paging.PageInvalid")]
    [InlineData("?pageSize=101", "Paging.PageSizeInvalid")]
    [InlineData("?pageSize=0", "Paging.PageSizeInvalid")]
    public async Task ListMembers_InvalidPaging_Returns400(string queryString, string expectedCode)
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SendAsync(client, token, HttpMethod.Get, MembersPath + queryString);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FirstFieldErrorCodeAsync(response)).ShouldBe(expectedCode);
    }

    [Fact]
    public async Task ListMembers_NoFilter_IncludesInactiveMembers()
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا", "09121234567");
        var inactive = await CreateMemberAsync(client, token, "علی", "09351234567");
        using var deactivated = await SendAsync(client, token, HttpMethod.Post, $"{MembersPath}/{inactive.Id}/deactivate");

        (await ListAsync(client, token, "")).TotalCount.ShouldBe(2);
        (await ListAsync(client, token, "?isActive=true")).Items.Single().FullName.ShouldBe("رضا");
        (await ListAsync(client, token, "?isActive=false")).Items.Single().FullName.ShouldBe("علی");
    }

    // ---- Name search ----

    [Fact]
    public async Task ListMembers_SearchTypedWithArabicYe_FindsTheNameStoredWithPersianYe()
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "علی رضایی", "09121234567");
        await CreateMemberAsync(client, token, "مریم", "09351234567");

        var result = await SearchAsync(client, token, $"عل{ArabicYe}");

        result.Items.Single().FullName.ShouldBe("علی رضایی");
    }

    [Fact]
    public async Task ListMembers_SearchWithPersianYe_FindsTheNameTypedWithArabicLetters()
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, $"{ArabicKaf}ر{ArabicYe}م", "09121234567");

        var result = await SearchAsync(client, token, "کریم");

        result.Items.Single().PhoneNumber.ShouldBe("+989121234567");
    }

    [Fact]
    public async Task ListMembers_SearchWithHalfSpace_MatchesANameWithAnOrdinarySpace()
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "محمد مهدی", "09121234567");

        var result = await SearchAsync(client, token, $"محمد{HalfSpace}مهدی");

        result.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task ListMembers_PartOfTheFamilyName_FindsTheMember()
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا احمدی", "09121234567");
        await CreateMemberAsync(client, token, "علی محمدی", "09351234567");

        var result = await SearchAsync(client, token, "احم");

        result.Items.Single().FullName.ShouldBe("رضا احمدی");
    }

    [Fact]
    public async Task ListMembers_LatinNameInAnotherCase_FindsTheMember()
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "Sara Smith", "09121234567");

        var result = await SearchAsync(client, token, "SMI");

        result.Items.Single().FullName.ShouldBe("Sara Smith");
    }

    [Theory]
    [InlineData("%%")]
    [InlineData("__")]
    public async Task ListMembers_SearchForAWildcardCharacter_DoesNotMatchEveryone(string search)
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا", "09121234567");
        await CreateMemberAsync(client, token, "علی", "09351234567");

        var result = await SearchAsync(client, token, search);

        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task ListMembers_SearchMatchingNobody_ReturnsAnEmptyPage()
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا", "09121234567");

        var result = await SearchAsync(client, token, "ناشناس");

        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Theory]
    [InlineData("ع")]
    [InlineData(" ع ")]
    public async Task ListMembers_OneCharacterSearch_Returns400SearchTooShort(string search)
    {
        var (client, token) = await StaffClientAsync();

        using var response = await SendAsync(client, token, HttpMethod.Get, $"{MembersPath}?search={Uri.EscapeDataString(search)}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FirstFieldErrorCodeAsync(response)).ShouldBe("Members.SearchTooShort");
    }

    [Fact]
    public async Task ListMembers_BlankSearch_ListsEveryone()
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا", "09121234567");

        (await SearchAsync(client, token, "   ")).TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task ListMembers_SearchCombinedWithStatus_AppliesBoth()
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "علی رضایی", "09121234567");
        var inactive = await CreateMemberAsync(client, token, "علی محمدی", "09351234567");
        using var deactivated = await SendAsync(client, token, HttpMethod.Post, $"{MembersPath}/{inactive.Id}/deactivate");

        var result = await ListAsync(client, token, $"?search={Uri.EscapeDataString("علی")}&isActive=false");

        result.Items.Single().FullName.ShouldBe("علی محمدی");
    }

    // ---- Phone search ----

    [Theory]
    [InlineData("09121234567")]
    [InlineData("۰۹۱۲۱۲۳۴۵۶۷")]
    [InlineData("٠٩١٢١٢٣٤٥٦٧")]
    [InlineData("+98 912 123 4567")]
    [InlineData("0098-912-123-4567")]
    [InlineData("9121234567")]
    public async Task ListMembers_WholePhoneInAnyFormat_FindsTheMember(string search)
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا", "09121234567");
        await CreateMemberAsync(client, token, "علی", "09351234567");

        var result = await SearchAsync(client, token, search);

        result.Items.Single().FullName.ShouldBe("رضا");
    }

    [Theory]
    [InlineData("4567")]
    [InlineData("۴۵۶۷")]
    [InlineData("0912 123")]
    [InlineData("912123")]
    public async Task ListMembers_PartOfAPhone_FindsTheMember(string search)
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا", "09121234567");
        await CreateMemberAsync(client, token, "علی", "09359876543");

        var result = await SearchAsync(client, token, search);

        result.Items.Single().FullName.ShouldBe("رضا");
    }

    [Theory]
    [InlineData("456")]
    [InlineData("0000")]
    [InlineData("02188776655")]
    public async Task ListMembers_UnusablePhoneSearch_ReturnsAnEmptyPageNotAnError(string search)
    {
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا", "09121234567");

        var result = await SearchAsync(client, token, search);

        result.TotalCount.ShouldBe(0);
    }

    // ---- Database ----

    [Fact]
    public async Task Members_SearchColumns_HaveTrigramIndexes()
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            "SELECT indexname FROM pg_indexes WHERE tablename = 'members' AND indexdef LIKE '%gin_trgm_ops%' ORDER BY indexname",
            connection);

        var names = new List<string>();
        await using (var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            {
                names.Add(reader.GetString(0));
            }
        }

        names.ShouldBe(["ix_members_normalized_full_name", "ix_members_phone_number_trgm"]);
    }

    // ---- Helpers ----

    private async Task<(HttpClient Client, string Token)> StaffClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("staff", TestUsers.Password));
    }

    private static async Task<MemberResponse> CreateMemberAsync(HttpClient client, string token, string fullName, string phoneNumber)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, MembersPath) { Content = JsonContent.Create(new { fullName, phoneNumber }) };
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MemberResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<PagedResponse<MemberResponse>> SearchAsync(HttpClient client, string token, string search) =>
        ListAsync(client, token, $"?search={Uri.EscapeDataString(search)}");

    private static async Task<PagedResponse<MemberResponse>> ListAsync(HttpClient client, string token, string queryString)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, MembersPath + queryString);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<PagedResponse<MemberResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path) =>
        client.SendAsync(new HttpRequestMessage(method, path).WithBearer(token), TestContext.Current.CancellationToken);

    private static async Task<string?> FirstFieldErrorCodeAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        return body.RootElement.GetProperty("errors").EnumerateObject().First().Value[0].GetProperty("code").GetString();
    }

    private async Task<Member> LoadMemberAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Members
            .AsNoTracking()
            .SingleAsync(member => member.Id == id, TestContext.Current.CancellationToken);
    }
}
