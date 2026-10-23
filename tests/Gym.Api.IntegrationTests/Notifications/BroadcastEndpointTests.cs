using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common.Paging;
using Gym.Application.Notifications.Broadcasts;
using Gym.Application.Notifications.Broadcasts.GetBroadcastAudience;
using Gym.Application.Notifications.Broadcasts.PreviewBroadcast;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The broadcast's endpoints under <c>/api/sms/broadcasts</c> (BUSINESS_RULES.md §10 <i>The
/// broadcast</i>, task 10.13). Owner only.
/// </summary>
/// <remarks>
/// The API runs on the real clock, so here only what holds at any hour: who may call them, the
/// figures, and the refusals that come before the sending hours. Sending, the job and «ادامه ارسال»
/// are in <see cref="BroadcastTests"/>, on a pinned clock.
/// </remarks>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class BroadcastEndpointTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Path = "/api/sms/broadcasts";

    private static int _phoneSuffix;

    private string? _ownerToken;

    [Theory]
    [InlineData("GET", Path)]
    [InlineData("GET", Path + "/audiences")]
    [InlineData("POST", Path + "/preview")]
    [InlineData("POST", Path)]
    [InlineData("POST", Path + "/0192a1b2-0000-7000-8000-000000000001/resume")]
    public async Task Endpoint_AsStaff_Returns403(string method, string path)
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "staff", role: Roles.Staff);
        using var client = Fixture.CreateClient();
        var token = await client.LoginForAccessTokenAsync("staff", TestUsers.Password);
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = method == "POST" ? JsonContent.Create(new { text = "متن", audience = "ActiveMembers" }) : null,
        };

        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_WithoutToken_Returns401()
    {
        using var client = Fixture.CreateClient();

        using var response = await client.GetAsync(Path, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task List_Nothing_IsAnEmptyPage()
    {
        using var response = await SendAsync(HttpMethod.Get, Path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.Content.ReadFromJsonAsync<PagedResponse<BroadcastResponse>>(TestContext.Current.CancellationToken);
        page.ShouldNotBeNull().TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task Audiences_BothGroups_GiveTheirCountAndLongestFirstName()
    {
        await AddMemberAsync("علی رضایی");
        await AddMemberAsync("سید عبدالرحمن کریمی");

        using var response = await SendAsync(HttpMethod.Get, Path + "/audiences");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<BroadcastAudiencesResponse>(TestContext.Current.CancellationToken))
            .ShouldBe(new BroadcastAudiencesResponse(
                new BroadcastGroupResponse(2, "عبدالرحمن"),
                new BroadcastGroupResponse(0, string.Empty)));
    }

    [Fact]
    public async Task Preview_Text_GivesTheFigures()
    {
        await AddMemberAsync("علی رضایی");

        using var response = await SendAsync(HttpMethod.Post, Path + "/preview", new { text = "  فردا تعطیل است.  ", audience = "ActiveMembers" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = (await response.Content.ReadFromJsonAsync<BroadcastPreviewResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        preview.Recipients.ShouldBe(1);
        preview.TotalParts.ShouldBe(1);
        preview.SampleText.ShouldBe("علی عزیز، فردا تعطیل است.\nلغو11");
    }

    [Fact]
    public async Task Preview_BlankText_Returns400TextRequired()
    {
        using var response = await SendAsync(HttpMethod.Post, Path + "/preview", new { text = " ", audience = "ActiveMembers" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadErrorCodeAsync()).ShouldBe("Sms.BroadcastTextRequired");
    }

    [Fact]
    public async Task Send_UnknownAudience_Returns400()
    {
        using var response = await SendAsync(HttpMethod.Post, Path, new { text = "متن", audience = "Everyone" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Send_BroadcastOff_Returns422AndSavesNothing()
    {
        await AddMemberAsync("علی رضایی");

        // The settings start off; the switches are checked before the hours, so this holds at any time.
        using var response = await SendAsync(HttpMethod.Post, Path, new { text = "متن", audience = "ActiveMembers" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe("Sms.BroadcastOff");
        await using var scope = Fixture.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().SmsBroadcasts.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resume_UnknownId_Returns404()
    {
        using var response = await SendAsync(HttpMethod.Post, $"{Path}/{Guid.CreateVersion7()}/resume");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe("Sms.BroadcastNotFound");
    }

    // ---- Helpers ----

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null)
    {
        var token = await OwnerTokenAsync();
        using var client = Fixture.CreateClient();
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };

        return await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
    }

    private async Task<string> OwnerTokenAsync()
    {
        if (_ownerToken is null)
        {
            await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
            using var client = Fixture.CreateClient();
            _ownerToken = await client.LoginForAccessTokenAsync("owner", TestUsers.Password);
        }

        return _ownerToken;
    }

    private async Task AddMemberAsync(string fullName)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(TestMembers.Seed(fullName, $"+98915{suffix:D7}"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
