using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Attendances;
using Gym.Application.Common.Paging;
using Gym.Application.Members;
using Gym.Application.Subscriptions;
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
    public async Task GetMember_MemberWithABirthDate_ReturnsTheSameDateAsCreate()
    {
        // The record equality above compares it too; this says outright that the stored date is
        // the one that was sent, not a default.
        var (client, token) = await StaffClientAsync();
        var created = await CreateMemberAsync(client, token, "رضا احمدی", "09121234567", birthDate: "1991-08-03");

        using var response = await SendAsync(client, token, HttpMethod.Get, $"{MembersPath}/{created.Id}");

        var member = (await response.Content.ReadFromJsonAsync<MemberResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
        member.BirthDate.ShouldBe(new DateOnly(1991, 8, 3));
    }

    [Fact]
    public async Task ListMembers_MemberWithABirthDate_IncludesIt()
    {
        // The list is served by the same Projection expression.
        var (client, token) = await StaffClientAsync();
        await CreateMemberAsync(client, token, "رضا احمدی", "09121234567", birthDate: "1991-08-03");

        var page = await ListAsync(client, token, "");

        page.Items.Single().BirthDate.ShouldBe(new DateOnly(1991, 8, 3));
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

    // ---- Debt (task 4.7: what the member owes, on the front desk's list) ----

    [Fact]
    public async Task ListMembers_MemberWithNoSubscription_DebtIsZero()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");

        var listed = await SingleAsync(client, token, member.Id);

        listed.Debt.ShouldBe(0m);
    }

    [Fact]
    public async Task ListMembers_MemberWithAFullyPaidSubscription_DebtIsZero()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var sold = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await PayAsync(client, token, sold.Id, 900_000m);

        var listed = await SingleAsync(client, token, member.Id);

        listed.Debt.ShouldBe(0m);
    }

    [Fact]
    public async Task ListMembers_MemberWithAPartiallyPaidSubscription_DebtIsWhatIsLeft()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var sold = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await PayAsync(client, token, sold.Id, 300_000m);

        var listed = await SingleAsync(client, token, member.Id);

        listed.Debt.ShouldBe(600_000m);
    }

    [Fact]
    public async Task ListMembers_MemberWithOnlyACancelledUnpaidSubscription_DebtIsZero()
    {
        // Cancelling is how the gym already says it is not chasing that money (BUSINESS_RULES.md
        // §4 Cancel), so a cancelled, never-paid subscription must not still flag the member.
        var (client, token) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var sold = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        var cancelRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/subscriptions/{sold.Id}/cancel")
        {
            Content = JsonContent.Create(new { reason = "اشتباه ثبت شد" }),
        };
        using var cancelled = await ownerClient.SendAsync(cancelRequest.WithBearer(ownerToken), TestContext.Current.CancellationToken);
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);

        var listed = await SingleAsync(client, token, member.Id);

        listed.Debt.ShouldBe(0m);
    }

    // ---- Debtors only (task 6.5.20: the «بدهکار» filter) ----

    [Fact]
    public async Task ListMembers_DebtorsOnly_ListsOnlyMembersWhoOweSomething()
    {
        var (client, token) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var partlyPaid = await CreateMemberAsync(client, token, "الف بدهکار", "09121234561");
        var unpaid = await CreateMemberAsync(client, token, "ب بدهکار", "09121234562");
        await CreateMemberAsync(client, token, "پ بدون اشتراک", "09121234563");
        var fullyPaid = await CreateMemberAsync(client, token, "ت تسویه", "09121234564");
        var cancelledOnly = await CreateMemberAsync(client, token, "ث لغو شده", "09121234565");

        var first = await SellSubscriptionAsync(client, token, partlyPaid.Id, 900_000m);
        await PayAsync(client, token, first.Id, 300_000m);
        await SellSubscriptionAsync(client, token, unpaid.Id, 900_000m);
        var paid = await SellSubscriptionAsync(client, token, fullyPaid.Id, 900_000m);
        await PayAsync(client, token, paid.Id, 900_000m);
        var cancelled = await SellSubscriptionAsync(client, token, cancelledOnly.Id, 900_000m);
        var cancelRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/subscriptions/{cancelled.Id}/cancel")
        {
            Content = JsonContent.Create(new { reason = "اشتباه ثبت شد" }),
        };
        using var cancelResponse = await ownerClient.SendAsync(cancelRequest.WithBearer(ownerToken), TestContext.Current.CancellationToken);
        cancelResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await ListAsync(client, token, "?debtorsOnly=true");

        page.TotalCount.ShouldBe(2);
        page.Items.Select(member => member.Id).ShouldBe([partlyPaid.Id, unpaid.Id]);
        page.Items.Select(member => member.Debt).ShouldBe([600_000m, 900_000m]);
    }

    [Fact]
    public async Task ListMembers_DebtorsOnlyWithPaging_CountsDebtorsBeforePaging()
    {
        // The filter must run in the query, not on the page afterwards: a page of 1 still reports
        // both debtors, and the non-debtor named between them does not take a slot.
        var (client, token) = await StaffClientAsync();
        var first = await CreateMemberAsync(client, token, "الف", "09121234561");
        await CreateMemberAsync(client, token, "ب", "09121234562");
        var second = await CreateMemberAsync(client, token, "پ", "09121234563");
        await SellSubscriptionAsync(client, token, first.Id, 900_000m);
        await SellSubscriptionAsync(client, token, second.Id, 900_000m);

        var page1 = await ListAsync(client, token, "?debtorsOnly=true&page=1&pageSize=1");
        var page2 = await ListAsync(client, token, "?debtorsOnly=true&page=2&pageSize=1");

        page1.TotalCount.ShouldBe(2);
        page1.Items.Single().Id.ShouldBe(first.Id);
        page2.Items.Single().Id.ShouldBe(second.Id);
    }

    [Fact]
    public async Task ListMembers_DebtorsOnlyAndInactive_ListsOnlyInactiveDebtors()
    {
        var (client, token) = await StaffClientAsync();
        var activeDebtor = await CreateMemberAsync(client, token, "رضا", "09121234561");
        var inactiveDebtor = await CreateMemberAsync(client, token, "علی", "09121234562");
        await CreateMemberAsync(client, token, "مریم", "09121234563");
        await SellSubscriptionAsync(client, token, activeDebtor.Id, 900_000m);
        await SellSubscriptionAsync(client, token, inactiveDebtor.Id, 900_000m);
        await PostOkAsync(client, token, $"{MembersPath}/{inactiveDebtor.Id}/deactivate");

        var page = await ListAsync(client, token, "?debtorsOnly=true&isActive=false");

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(inactiveDebtor.Id);
    }

    [Fact]
    public async Task ListMembers_DebtorsOnlyWithSearch_ListsOnlyMatchingDebtors()
    {
        var (client, token) = await StaffClientAsync();
        var reza = await CreateMemberAsync(client, token, "رضا احمدی", "09121234561");
        var ali = await CreateMemberAsync(client, token, "علی احمدی", "09121234562");
        await SellSubscriptionAsync(client, token, reza.Id, 900_000m);
        await SellSubscriptionAsync(client, token, ali.Id, 900_000m);

        var page = await ListAsync(client, token, $"?debtorsOnly=true&search={Uri.EscapeDataString("رضا")}");

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(reza.Id);
    }

    // ---- Inside the gym (the front desk's check-in button) ----

    [Fact]
    public async Task ListMembers_MemberNeverCheckedIn_HasNoCurrentVisit()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");

        (await SingleAsync(client, token, member.Id)).CurrentVisit.ShouldBeNull();
    }

    [Fact]
    public async Task ListMembers_MemberCheckedIn_HasTheOpenVisitWithItsLocker()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var other = await CreateMemberAsync(client, token, "علی", "09351234567");
        await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        var attendance = await TestLockers.CheckInOkAsync(client, token, member.Id, lockerNumber: 7);

        var visit = (await SingleAsync(client, token, member.Id)).CurrentVisit.ShouldNotBeNull();
        visit.AttendanceId.ShouldBe(attendance.Id);
        visit.LockerNumber.ShouldBe(7);
        visit.UsesReservePlace.ShouldBeFalse();
        visit.CheckedInAt.ShouldBe(attendance.CheckedInAt, TimeSpan.FromMicroseconds(1));
        (await SingleAsync(client, token, other.Id)).CurrentVisit.ShouldBeNull();
    }

    [Fact]
    public async Task ListMembers_MemberCheckedOut_HasNoCurrentVisit()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        var attendance = await CheckInAsync(client, token, member.Id);
        using var checkedOut = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{attendance.Id}/check-out");
        checkedOut.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await SingleAsync(client, token, member.Id)).CurrentVisit.ShouldBeNull();
    }

    // ---- Frozen (shown next to the member's status on the list) ----

    [Fact]
    public async Task ListMembers_MemberWithAFrozenSubscription_IsFrozen()
    {
        var (client, token) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var other = await CreateMemberAsync(client, token, "علی", "09351234567");
        var sold = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await SellSubscriptionAsync(client, token, other.Id, 900_000m);
        await PostOkAsync(ownerClient, ownerToken, $"/api/subscriptions/{sold.Id}/freeze");

        (await SingleAsync(client, token, member.Id)).IsFrozen.ShouldBeTrue();
        (await SingleAsync(client, token, other.Id)).IsFrozen.ShouldBeFalse();
    }

    [Fact]
    public async Task ListMembers_MemberUnfrozen_IsNotFrozen()
    {
        var (client, token) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var sold = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await PostOkAsync(ownerClient, ownerToken, $"/api/subscriptions/{sold.Id}/freeze");
        await PostOkAsync(ownerClient, ownerToken, $"/api/subscriptions/{sold.Id}/unfreeze");

        (await SingleAsync(client, token, member.Id)).IsFrozen.ShouldBeFalse();
    }

    [Fact]
    public async Task ListMembers_FrozenSubscriptionThenCancelled_IsNotFrozen()
    {
        // A cancelled subscription is Cancelled, not Frozen (Subscription.GetStatus), even though
        // its FrozenSince is still set.
        var (client, token) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var sold = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await PostOkAsync(ownerClient, ownerToken, $"/api/subscriptions/{sold.Id}/freeze");
        var cancelRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/subscriptions/{sold.Id}/cancel")
        {
            Content = JsonContent.Create(new { reason = "اشتباه ثبت شد" }),
        };
        using var cancelled = await ownerClient.SendAsync(cancelRequest.WithBearer(ownerToken), TestContext.Current.CancellationToken);
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await SingleAsync(client, token, member.Id)).IsFrozen.ShouldBeFalse();
    }

    // ---- Order: the latest visit first (BUSINESS_RULES.md §2) ----

    [Fact]
    public async Task ListMembers_SomeMembersVisited_LatestVisitFirstAndNeverVisitedLastByName()
    {
        var (client, token) = await StaffClientAsync();
        var hadi = await CreateMemberAsync(client, token, "هادی", "09121234560");
        var bahram = await CreateMemberAsync(client, token, "بهرام", "09121234561");
        var elham = await CreateMemberAsync(client, token, "الهام", "09121234562");
        var maryam = await CreateMemberAsync(client, token, "مریم", "09121234563");
        await SellSubscriptionAsync(client, token, hadi.Id, 900_000m);
        await SellSubscriptionAsync(client, token, maryam.Id, 900_000m);
        var older = await CheckInAndOutAsync(client, token, hadi.Id);
        var latest = await CheckInAndOutAsync(client, token, maryam.Id);
        await SetCheckedInAtAsync(older, daysAgo: 3);
        await SetCheckedInAtAsync(latest, daysAgo: 1);

        var page = await ListAsync(client, token, "");

        page.Items.Select(member => member.Id).ShouldBe([maryam.Id, hadi.Id, elham.Id, bahram.Id]);
    }

    [Fact]
    public async Task ListMembers_LatestCheckInCancelled_SortsByTheVisitBeforeIt()
    {
        var (client, token) = await StaffClientAsync();
        var reza = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var ali = await CreateMemberAsync(client, token, "علی", "09351234567");
        await SellSubscriptionAsync(client, token, reza.Id, 900_000m);
        await SellSubscriptionAsync(client, token, ali.Id, 900_000m);
        await SetCheckedInAtAsync(await CheckInAndOutAsync(client, token, reza.Id), daysAgo: 3);
        await SetCheckedInAtAsync(await CheckInAndOutAsync(client, token, ali.Id), daysAgo: 2);
        var mistake = await CheckInAsync(client, token, reza.Id);
        using var cancelled = await SendAsync(client, token, HttpMethod.Post, $"/api/attendance/{mistake.Id}/cancel", CancelCheckInBody.KeepPurchases);
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);

        var page = await ListAsync(client, token, "");

        page.Items.Select(member => member.Id).ShouldBe([ali.Id, reza.Id]);
    }

    // ---- Tags and the sessions bar (BUSINESS_RULES.md §2) ----

    [Fact]
    public async Task ListMembers_LatestVisitOnASingleVisit_IsTaggedAndOthersAreNot()
    {
        var (client, token) = await StaffClientAsync();
        var walkIn = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var member = await CreateMemberAsync(client, token, "علی", "09351234567");
        var neverCame = await CreateMemberAsync(client, token, "مریم", "09131234567");
        await SellSingleVisitAsync(client, token, walkIn.Id);
        await CheckInAsync(client, token, walkIn.Id);
        await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await CheckInAsync(client, token, member.Id);

        (await SingleAsync(client, token, walkIn.Id)).LastVisitWasSingleSession.ShouldBeTrue();
        (await SingleAsync(client, token, member.Id)).LastVisitWasSingleSession.ShouldBeFalse();
        (await SingleAsync(client, token, neverCame.Id)).LastVisitWasSingleSession.ShouldBeFalse();
    }

    [Fact]
    public async Task ListMembers_SingleVisitThenAVisitOnAPlan_IsNotTagged()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        await SellSingleVisitAsync(client, token, member.Id);
        await SetCheckedInAtAsync(await CheckInAndOutAsync(client, token, member.Id), daysAgo: 1);
        await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await CheckInAsync(client, token, member.Id);

        (await SingleAsync(client, token, member.Id)).LastVisitWasSingleSession.ShouldBeFalse();
    }

    [Fact]
    public async Task ListMembers_ActivePlanWithAVisit_ShowsItsSessionsAndHasNotEnded()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await CheckInAsync(client, token, member.Id);

        var row = await SingleAsync(client, token, member.Id);

        row.Plan.ShouldBe(new MemberPlanSessions(TotalSessions: 10, UsedSessions: 1, RemainingSessions: 9));
        row.PlanEnded.ShouldBeFalse();
    }

    [Fact]
    public async Task ListMembers_PlanExpiredAndNothingAfterIt_HasEndedAndShowsThatPlan()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var sold = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await MoveIntoThePastAsync(sold.Id, days: 60);

        var row = await SingleAsync(client, token, member.Id);

        row.PlanEnded.ShouldBeTrue();
        row.Plan.ShouldBe(new MemberPlanSessions(TotalSessions: 10, UsedSessions: 0, RemainingSessions: 10));
    }

    [Fact]
    public async Task ListMembers_PlanExpiredAndRenewed_HasNotEndedAndShowsTheNewPlan()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var expired = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await MoveIntoThePastAsync(expired.Id, days: 60);
        await SellSubscriptionAsync(client, token, member.Id, 1_200_000m, sessions: 12);

        var row = await SingleAsync(client, token, member.Id);

        row.PlanEnded.ShouldBeFalse();
        row.Plan.ShouldNotBeNull().TotalSessions.ShouldBe(12);
    }

    [Fact]
    public async Task ListMembers_DeactivatedMemberWithAnExpiredPlan_HasNotEnded()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var sold = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await MoveIntoThePastAsync(sold.Id, days: 60);
        await PostOkAsync(client, token, $"{MembersPath}/{member.Id}/deactivate");

        (await SingleAsync(client, token, member.Id)).PlanEnded.ShouldBeFalse();
    }

    [Fact]
    public async Task ListMembers_OnlySingleVisits_HasNoPlanAndHasNotEnded()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var visit = await SellSingleVisitAsync(client, token, member.Id);
        await MoveIntoThePastAsync(visit.Id, days: 10);

        var row = await SingleAsync(client, token, member.Id);

        row.Plan.ShouldBeNull();
        row.PlanEnded.ShouldBeFalse();
    }

    [Fact]
    public async Task ListMembers_PlanExhaustedBeforeItsEndDate_HasEnded()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var sold = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await ExecuteSqlAsync($"UPDATE subscriptions SET used_sessions = total_sessions WHERE id = '{sold.Id}'");

        (await SingleAsync(client, token, member.Id)).PlanEnded.ShouldBeTrue();
    }

    [Fact]
    public async Task ListMembers_FrozenPlanPastItsEndDate_HasNotEnded()
    {
        var (client, token) = await StaffClientAsync();
        var (ownerClient, ownerToken) = await OwnerClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var sold = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await PostOkAsync(ownerClient, ownerToken, $"/api/subscriptions/{sold.Id}/freeze");
        await MoveIntoThePastAsync(sold.Id, days: 60);

        (await SingleAsync(client, token, member.Id)).PlanEnded.ShouldBeFalse();
    }

    // ---- Never both tags: the more recent one (BUSINESS_RULES.md §2) ----

    [Fact]
    public async Task ListMembers_PlanExpiredThenASingleVisit_IsTaggedSingleSessionOnly()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        await MoveIntoThePastAsync((await SellSubscriptionAsync(client, token, member.Id, 900_000m)).Id, days: 60);
        await SellSingleVisitAsync(client, token, member.Id);
        await CheckInAsync(client, token, member.Id);

        var row = await SingleAsync(client, token, member.Id);

        row.LastVisitWasSingleSession.ShouldBeTrue();
        row.PlanEnded.ShouldBeFalse();
        (await ListAsync(client, token, "?singleSessionOnly=true")).TotalCount.ShouldBe(1);
        (await ListAsync(client, token, "?planEndedOnly=true")).TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task ListMembers_PlanUsedUpThenASingleVisit_IsTaggedSingleSessionOnly()
    {
        // A plan whose sessions ran out ended at its last session, before the single visit, even
        // though its end date is still ahead.
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var plan = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await ExecuteSqlAsync($"UPDATE subscriptions SET used_sessions = total_sessions WHERE id = '{plan.Id}'");
        await SellSingleVisitAsync(client, token, member.Id);
        await CheckInAsync(client, token, member.Id);

        var row = await SingleAsync(client, token, member.Id);

        row.LastVisitWasSingleSession.ShouldBeTrue();
        row.PlanEnded.ShouldBeFalse();
    }

    [Fact]
    public async Task ListMembers_SingleVisitThenAPlanThatExpiredUnused_IsTaggedPlanEndedOnly()
    {
        var (client, token) = await StaffClientAsync();
        var member = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var singleVisit = await SellSingleVisitAsync(client, token, member.Id);
        var visit = await CheckInAndOutAsync(client, token, member.Id);
        var plan = await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        // The single visit 70 days ago; the plan bought after it, from 60 days ago, never used.
        await MoveIntoThePastAsync(singleVisit.Id, days: 70);
        await SetCheckedInAtAsync(visit, daysAgo: 70);
        await MoveIntoThePastAsync(plan.Id, days: 60);

        var row = await SingleAsync(client, token, member.Id);

        row.PlanEnded.ShouldBeTrue();
        row.LastVisitWasSingleSession.ShouldBeFalse();
        (await ListAsync(client, token, "?planEndedOnly=true")).TotalCount.ShouldBe(1);
        (await ListAsync(client, token, "?singleSessionOnly=true")).TotalCount.ShouldBe(0);
    }

    // ---- Tag filters (BUSINESS_RULES.md §2) ----

    [Fact]
    public async Task ListMembers_SingleSessionOnly_ListsOnlyTaggedMembersAndCountsThem()
    {
        var (client, token) = await StaffClientAsync();
        var walkIn = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var member = await CreateMemberAsync(client, token, "علی", "09351234567");
        await CreateMemberAsync(client, token, "مریم", "09131234567");
        await SellSingleVisitAsync(client, token, walkIn.Id);
        await CheckInAsync(client, token, walkIn.Id);
        await SellSubscriptionAsync(client, token, member.Id, 900_000m);
        await CheckInAsync(client, token, member.Id);

        var page = await ListAsync(client, token, "?singleSessionOnly=true");

        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(walkIn.Id);
    }

    [Fact]
    public async Task ListMembers_PlanEndedOnly_ListsOnlyTaggedMembersAndCountsThem()
    {
        var (client, token) = await StaffClientAsync();
        var ended = await CreateMemberAsync(client, token, "رضا", "09121234567");
        var current = await CreateMemberAsync(client, token, "علی", "09351234567");
        var deactivated = await CreateMemberAsync(client, token, "مریم", "09131234567");
        await CreateMemberAsync(client, token, "هادی", "09141234567");
        await MoveIntoThePastAsync((await SellSubscriptionAsync(client, token, ended.Id, 900_000m)).Id, days: 60);
        await SellSubscriptionAsync(client, token, current.Id, 900_000m);
        await MoveIntoThePastAsync((await SellSubscriptionAsync(client, token, deactivated.Id, 900_000m)).Id, days: 60);
        await PostOkAsync(client, token, $"{MembersPath}/{deactivated.Id}/deactivate");

        var page = await ListAsync(client, token, "?planEndedOnly=true");

        page.TotalCount.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(ended.Id);
    }

    [Fact]
    public async Task ListMembers_PlanEndedOnlyWithSearch_AppliesBoth()
    {
        var (client, token) = await StaffClientAsync();
        var reza = await CreateMemberAsync(client, token, "رضا احمدی", "09121234567");
        var ali = await CreateMemberAsync(client, token, "علی احمدی", "09351234567");
        await MoveIntoThePastAsync((await SellSubscriptionAsync(client, token, reza.Id, 900_000m)).Id, days: 60);
        await MoveIntoThePastAsync((await SellSubscriptionAsync(client, token, ali.Id, 900_000m)).Id, days: 60);

        var page = await ListAsync(client, token, $"?planEndedOnly=true&search={Uri.EscapeDataString("رضا")}");

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(reza.Id);
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

    private static async Task<MemberResponse> CreateMemberAsync(
        HttpClient client, string token, string fullName, string phoneNumber, string birthDate = "1990-06-15")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, MembersPath)
        {
            Content = JsonContent.Create(new { fullName, phoneNumber, birthDate }),
        };
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MemberResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private async Task<(HttpClient Client, string Token)> OwnerClientAsync()
    {
        await TestUsers.CreateWithOwnPasswordAsync(Fixture, userName: "owner", role: Roles.Owner);
        var client = Fixture.CreateClient();

        return (client, await client.LoginForAccessTokenAsync("owner", TestUsers.Password));
    }

    private Task<TestPlan> AddPlanAsync(decimal price, int sessions = 10) => TestPlans.AddAsync(Fixture, sessions, price);

    /// <summary>Assigns a fresh subscription through the real endpoint, so its price is a plan's real, saved snapshot.</summary>
    private async Task<SubscriptionResponse> SellSubscriptionAsync(
        HttpClient client, string token, Guid memberId, decimal price, int sessions = 10)
    {
        var plan = await AddPlanAsync(price, sessions);
        var request = new HttpRequestMessage(HttpMethod.Post, $"{MembersPath}/{memberId}/subscriptions")
        {
            Content = JsonContent.Create(plan.Body),
        };
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task PayAsync(HttpClient client, string token, Guid subscriptionId, decimal amount)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/subscriptions/{subscriptionId}/payments")
        {
            Content = JsonContent.Create(new { amount, method = "Cash" }),
        };
        using var response = await client.SendAsync(request.WithBearer(token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private static async Task<AttendanceResponse> CheckInAsync(HttpClient client, string token, Guid memberId)
    {
        using var response = await TestLockers.CheckInAsync(client, token, memberId);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<AttendanceResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private async Task<SubscriptionResponse> SellSingleVisitAsync(HttpClient client, string token, Guid memberId)
    {
        await TestPlans.SetPricesAsync(Fixture, singleVisitPrice: 150_000m);
        using var response = await SendAsync(client, token, HttpMethod.Post, $"{MembersPath}/{memberId}/subscriptions/single-visit");
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        return (await response.Content.ReadFromJsonAsync<SubscriptionResponse>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    /// <summary>A whole visit, so the member can come in again.</summary>
    private static async Task<Guid> CheckInAndOutAsync(HttpClient client, string token, Guid memberId)
    {
        var attendance = await CheckInAsync(client, token, memberId);
        await PostOkAsync(client, token, $"/api/attendance/{attendance.Id}/check-out");

        return attendance.Id;
    }

    /// <summary>
    /// Check-ins made one after another are microseconds apart; moving one back makes the order a
    /// test reads from them certain.
    /// </summary>
    private Task SetCheckedInAtAsync(Guid attendanceId, int daysAgo) =>
        ExecuteSqlAsync($"UPDATE attendances SET checked_in_at = checked_in_at - interval '{daysAgo} days' WHERE id = '{attendanceId}'");

    /// <summary>Moves a whole subscription back, its length unchanged, as if it had been sold earlier.</summary>
    private Task MoveIntoThePastAsync(Guid subscriptionId, int days) =>
        ExecuteSqlAsync($"UPDATE subscriptions SET start_date = start_date - {days}, end_date = end_date - {days} WHERE id = '{subscriptionId}'");

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
            .ExecuteSqlRawAsync(sql, TestContext.Current.CancellationToken);
    }

    /// <summary>The one member's row from the list endpoint, filtered by search so paging never hides it.</summary>
    private static async Task<MemberResponse> SingleAsync(HttpClient client, string token, Guid memberId)
    {
        var page = await ListAsync(client, token, "?pageSize=100");

        return page.Items.Single(member => member.Id == memberId);
    }

    private static Task<PagedResponse<MemberResponse>> SearchAsync(HttpClient client, string token, string search) =>
        ListAsync(client, token, $"?search={Uri.EscapeDataString(search)}");

    private static async Task<PagedResponse<MemberResponse>> ListAsync(HttpClient client, string token, string queryString)
    {
        using var response = await SendAsync(client, token, HttpMethod.Get, MembersPath + queryString);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.Content.ReadFromJsonAsync<PagedResponse<MemberResponse>>(TestContext.Current.CancellationToken)).ShouldNotBeNull();
    }

    private static async Task PostOkAsync(HttpClient client, string token, string path)
    {
        using var response = await SendAsync(client, token, HttpMethod.Post, path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path) =>
        client.SendAsync(new HttpRequestMessage(method, path).WithBearer(token), TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string token, HttpMethod method, string path, object body) =>
        client.SendAsync(
            new HttpRequestMessage(method, path) { Content = JsonContent.Create(body) }.WithBearer(token),
            TestContext.Current.CancellationToken);

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
