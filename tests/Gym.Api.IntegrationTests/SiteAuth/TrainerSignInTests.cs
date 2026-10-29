using System.Net;
using System.Security.Claims;
using System.Text.Json;

using Gym.Api.Authorization;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.SiteAuth.SignIn;
using Gym.Application.Trainers.UpdateTrainer;
using Gym.Domain.Members;
using Gym.Domain.SiteAuth;
using Gym.Domain.Trainers;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.SiteAuth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Gym.Api.IntegrationTests.SiteAuth;

/// <summary>
/// Trainers on the site's sign-in (BUSINESS_RULES.md §15 <i>Trainers</i>): a trainer's phone signs
/// in without a sign-up, one phone that is both a member's and a trainer's opens both areas, and
/// the Owner stopping a trainer or changing their phone takes the trainer's area away.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class TrainerSignInTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Phone = "+989121234567";
    private const string OtherPhone = "+989351234567";

    // ---- Signing in ----

    [Fact]
    public async Task SignIn_ATrainersPhone_SignsInAsTheTrainerWithNoSignUp()
    {
        var trainer = await AddTrainerAsync("علی کریمی", Phone);
        using var client = Fixture.CreateClient();

        using var response = await client.SignInWithCodeAsync(Fixture, Phone);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("outcome").GetString().ShouldBe(nameof(SignInOutcome.SignedIn));
        var me = body.GetProperty("access").GetProperty("me");
        me.GetProperty("member").ValueKind.ShouldBe(JsonValueKind.Null);
        me.GetProperty("trainer").GetProperty("trainerId").GetGuid().ShouldBe(trainer.Id);
        me.GetProperty("trainer").GetProperty("fullName").GetString().ShouldBe("علی کریمی");
        Roles(body.GetProperty("access")).ShouldBe([SiteRoles.Trainer]);

        var account = (await AccountsAsync()).ShouldHaveSingleItem();
        account.TrainerId.ShouldBe(trainer.Id);
        account.MemberId.ShouldBeNull();
        (await MembersAsync()).ShouldBeEmpty("a trainer is not made a member by signing in.");
    }

    [Fact]
    public async Task SignIn_APhoneThatIsAMembersAndATrainers_OneAccountOpensBoth()
    {
        var member = await AddMemberAsync(Phone);
        var trainer = await AddTrainerAsync("علی کریمی", Phone);
        using var client = Fixture.CreateClient();

        using var response = await client.SignInWithCodeAsync(Fixture, Phone);

        var access = (await response.ReadJsonAsync()).GetProperty("access");
        access.GetProperty("me").GetProperty("member").GetProperty("memberId").GetGuid().ShouldBe(member.Id);
        access.GetProperty("me").GetProperty("trainer").GetProperty("trainerId").GetGuid().ShouldBe(trainer.Id);
        Roles(access).ShouldBe([SiteRoles.Member, SiteRoles.Trainer], ignoreOrder: true);

        var account = (await AccountsAsync()).ShouldHaveSingleItem();
        account.MemberId.ShouldBe(member.Id);
        account.TrainerId.ShouldBe(trainer.Id);
    }

    [Fact]
    public async Task SignIn_AMembersAccountWhenTheOwnerLaterMakesThemATrainer_GainsTheTrainerOnTheSameAccount()
    {
        var member = await AddMemberAsync(Phone);
        using var client = Fixture.CreateClient();
        using var first = await client.SignInWithCodeAsync(Fixture, Phone);
        var trainer = await AddTrainerAsync("علی کریمی", Phone);

        using var second = await client.SignInWithCodeAsync(Fixture, Phone);

        Roles((await second.ReadJsonAsync()).GetProperty("access")).ShouldBe([SiteRoles.Member, SiteRoles.Trainer], ignoreOrder: true);
        var account = (await AccountsAsync()).ShouldHaveSingleItem();
        account.MemberId.ShouldBe(member.Id);
        account.TrainerId.ShouldBe(trainer.Id);
    }

    [Fact]
    public async Task SignIn_ATrainersAccountWhenTheDeskLaterRegistersThemAsAMember_TakesTheMemberIn()
    {
        var trainer = await AddTrainerAsync("علی کریمی", Phone);
        using var client = Fixture.CreateClient();
        using var first = await client.SignInWithCodeAsync(Fixture, Phone);
        var member = await AddMemberAsync(Phone);

        using var second = await client.SignInWithCodeAsync(Fixture, Phone);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var account = (await AccountsAsync()).ShouldHaveSingleItem();
        account.MemberId.ShouldBe(member.Id);
        account.TrainerId.ShouldBe(trainer.Id);
    }

    [Fact]
    public async Task SignIn_AStoppedTrainerWhoIsNotAMember_GetsTheSignUpScreen()
    {
        var trainer = await AddTrainerAsync("علی کریمی", Phone);
        await StopAsync(trainer.Id);
        using var client = Fixture.CreateClient();

        using var response = await client.SignInWithCodeAsync(Fixture, Phone);

        (await response.ReadJsonAsync()).GetProperty("outcome").GetString().ShouldBe(nameof(SignInOutcome.NeedsProfile));
        (await AccountsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task SignIn_TrainerWhoseLinkSitsOnAMemberWhoChangedPhone_GetsAnAccountOfTheirOwn()
    {
        // One person, member and trainer with one phone; then the desk gives the member another
        // number. The phone is now the trainer's alone, and must not open that member's area.
        var member = await AddMemberAsync(Phone);
        var trainer = await AddTrainerAsync("علی کریمی", Phone);
        using var client = Fixture.CreateClient();
        using var first = await client.SignInWithCodeAsync(Fixture, Phone);
        await ChangeMemberPhoneAsync(member.Id, OtherPhone);

        using var second = await client.SignInWithCodeAsync(Fixture, Phone);

        var access = (await second.ReadJsonAsync()).GetProperty("access");
        access.GetProperty("me").GetProperty("member").ValueKind.ShouldBe(JsonValueKind.Null);
        Roles(access).ShouldBe([SiteRoles.Trainer]);

        var accounts = await AccountsAsync();
        accounts.Count.ShouldBe(2);
        accounts.Single(account => account.MemberId == member.Id).TrainerId.ShouldBeNull();
        accounts.Single(account => account.MemberId is null).TrainerId.ShouldBe(trainer.Id);
    }

    [Fact]
    public async Task SignIn_TheDeskGivesAMemberATrainersPhone_TheTrainerMovesToTheMembersAccount()
    {
        // Two accounts that both exist: a member's on another phone, a trainer-only one on Phone.
        // Then the desk gives the member Phone: one person, and the member's account wins.
        var member = await AddMemberAsync(OtherPhone);
        var trainer = await AddTrainerAsync("علی کریمی", Phone);
        using var client = Fixture.CreateClient();
        using var memberSignIn = await client.SignInWithCodeAsync(Fixture, OtherPhone);
        using var trainerSignIn = await client.SignInWithCodeAsync(Fixture, Phone);
        await ChangeMemberPhoneAsync(member.Id, Phone);

        using var response = await client.SignInWithCodeAsync(Fixture, Phone);

        Roles((await response.ReadJsonAsync()).GetProperty("access")).ShouldBe([SiteRoles.Member, SiteRoles.Trainer], ignoreOrder: true);
        var accounts = await AccountsAsync();
        accounts.Single(account => account.MemberId == member.Id).TrainerId.ShouldBe(trainer.Id);
        accounts.Single(account => account.MemberId is null).TrainerId.ShouldBeNull();

        using var emptied = await client.RefreshAsync(trainerSignIn.ReadSiteRefreshToken());
        emptied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "the emptied trainer-only account opens nothing.");
    }

    [Fact]
    public async Task SignUp_AStoppedTrainersPhone_TheirAccountTakesTheNewMemberWithNoTrainerRole()
    {
        var trainer = await AddTrainerAsync("علی کریمی", Phone);
        using var client = Fixture.CreateClient();
        using var asTrainer = await client.SignInWithCodeAsync(Fixture, Phone);
        await StopAsync(trainer.Id);
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);
        var ticket = (await signIn.ReadJsonAsync()).GetProperty("signUpTicket").GetString()!;

        using var response = await client.SignUpAsync(ticket, "علی کریمی", new DateOnly(1990, 1, 1));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Roles(await response.ReadJsonAsync()).ShouldBe([SiteRoles.Member]);
        var account = (await AccountsAsync()).ShouldHaveSingleItem();
        account.MemberId.ShouldBe((await MembersAsync()).ShouldHaveSingleItem().Id);
        account.TrainerId.ShouldBe(trainer.Id);
    }

    [Fact]
    public async Task SignIn_ADeactivatedMember_TokenHasNoMemberRole()
    {
        // §15: a deactivated member sees only the notice, through «me»; the member's area stays shut.
        var member = await AddMemberAsync(Phone);
        await DeactivateAsync(member.Id);
        using var client = Fixture.CreateClient();

        using var response = await client.SignInWithCodeAsync(Fixture, Phone);

        var access = (await response.ReadJsonAsync()).GetProperty("access");
        access.GetProperty("me").GetProperty("member").GetProperty("isActive").GetBoolean().ShouldBeFalse();
        Roles(access).ShouldBeEmpty();
    }

    [Fact]
    public async Task SignIn_ATrainerWhoseMembershipTheDeskDeactivated_SeesOnlyTheNotice()
    {
        // §15 (1405/08/06): a deactivated member who is also a trainer gets neither area.
        var member = await AddMemberAsync(Phone);
        await AddTrainerAsync("علی کریمی", Phone);
        await DeactivateAsync(member.Id);
        using var client = Fixture.CreateClient();

        using var response = await client.SignInWithCodeAsync(Fixture, Phone);

        var access = (await response.ReadJsonAsync()).GetProperty("access");
        access.GetProperty("me").GetProperty("member").GetProperty("isActive").GetBoolean().ShouldBeFalse();
        access.GetProperty("me").GetProperty("trainer").ValueKind.ShouldBe(JsonValueKind.Null);
        Roles(access).ShouldBeEmpty();
    }

    [Fact]
    public async Task Refresh_AfterTheDeskDeactivatesATrainersMembership_LosesTheTrainersArea()
    {
        var member = await AddMemberAsync(Phone);
        await AddTrainerAsync("علی کریمی", Phone);
        using var client = Fixture.CreateClient();
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);

        await DeactivateAsync(member.Id);
        using var refreshed = await client.RefreshAsync(signIn.ReadSiteRefreshToken());

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var access = await refreshed.ReadJsonAsync();
        access.GetProperty("me").GetProperty("trainer").ValueKind.ShouldBe(JsonValueKind.Null);
        Roles(access).ShouldBeEmpty();
    }

    // ---- The Owner stops the trainer ----

    [Fact]
    public async Task Me_AfterTheOwnerStopsTheTrainer_HasNoTrainerAtOnce()
    {
        var trainer = await AddTrainerAsync("علی کریمی", Phone);
        using var client = Fixture.CreateClient();
        var accessToken = await SignInForAccessTokenAsync(client);

        await StopAsync(trainer.Id);
        using var response = await client.GetMeAsync(accessToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await response.ReadJsonAsync();
        me.GetProperty("trainer").ValueKind.ShouldBe(JsonValueKind.Null);
        me.GetProperty("member").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Refresh_AfterTheOwnerStopsATrainerWhoIsAlsoAMember_KeepsOnlyTheMembersArea()
    {
        await AddMemberAsync(Phone);
        var trainer = await AddTrainerAsync("علی کریمی", Phone);
        using var client = Fixture.CreateClient();
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);

        await StopAsync(trainer.Id);
        using var refreshed = await client.RefreshAsync(signIn.ReadSiteRefreshToken());

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var access = await refreshed.ReadJsonAsync();
        access.GetProperty("me").GetProperty("trainer").ValueKind.ShouldBe(JsonValueKind.Null);
        Roles(access).ShouldBe([SiteRoles.Member]);
    }

    // ---- The Owner changes the trainer's phone ----

    [Fact]
    public async Task UpdateTrainer_NewPhone_EndsTheOldPhonesTrainerOnlySessions()
    {
        var trainer = await AddTrainerAsync("علی کریمی", Phone);
        using var client = Fixture.CreateClient();
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);

        (await ChangeTrainerPhoneAsync(trainer.Id, OtherPhone)).IsSuccess.ShouldBeTrue();
        using var refreshed = await client.RefreshAsync(signIn.ReadSiteRefreshToken());

        refreshed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await AccountsAsync()).ShouldHaveSingleItem().TrainerId.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateTrainer_NewPhone_AMemberOnTheOldPhoneKeepsTheirSessionWithoutTheTrainer()
    {
        await AddMemberAsync(Phone);
        var trainer = await AddTrainerAsync("علی کریمی", Phone);
        using var client = Fixture.CreateClient();
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);

        (await ChangeTrainerPhoneAsync(trainer.Id, OtherPhone)).IsSuccess.ShouldBeTrue();
        using var refreshed = await client.RefreshAsync(signIn.ReadSiteRefreshToken());

        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        Roles(await refreshed.ReadJsonAsync()).ShouldBe([SiteRoles.Member]);
    }

    [Fact]
    public async Task SignIn_TheTrainersNewPhone_SignsInAsTheTrainer()
    {
        var trainer = await AddTrainerAsync("علی کریمی", Phone);
        using var client = Fixture.CreateClient();
        using var first = await client.SignInWithCodeAsync(Fixture, Phone);
        (await ChangeTrainerPhoneAsync(trainer.Id, OtherPhone)).IsSuccess.ShouldBeTrue();

        using var second = await client.SignInWithCodeAsync(Fixture, OtherPhone);

        var me = (await second.ReadJsonAsync()).GetProperty("access").GetProperty("me");
        me.GetProperty("trainer").GetProperty("trainerId").GetGuid().ShouldBe(trainer.Id);
    }

    // ---- The site's policies ----

    [Theory]
    [InlineData(Policies.SiteMember, SiteRoles.Member, true)]
    [InlineData(Policies.SiteMember, SiteRoles.Trainer, false)]
    [InlineData(Policies.SiteTrainer, SiteRoles.Trainer, true)]
    [InlineData(Policies.SiteTrainer, SiteRoles.Member, false)]
    [InlineData(Policies.SiteAccount, SiteRoles.Trainer, true)]
    public async Task SitePolicy_ForRole_AllowsOnlyItsArea(string policy, string role, bool expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(SiteClaimNames.SiteAccount, Guid.CreateVersion7().ToString()), new Claim(SiteClaimNames.Role, role)],
            authenticationType: "Test"));
        await using var scope = Fixture.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, policy);

        result.Succeeded.ShouldBe(expected);
    }

    // ---- Helpers ----

    /// <summary>The roles in the access token of a sign-in's or a refresh's body.</summary>
    private static List<string> Roles(JsonElement access) =>
        [.. new JsonWebToken(access.GetProperty("accessToken").GetString()!).Claims
            .Where(claim => claim.Type == SiteClaimNames.Role)
            .Select(claim => claim.Value)];

    private async Task<string> SignInForAccessTokenAsync(HttpClient client)
    {
        using var signIn = await client.SignInWithCodeAsync(Fixture, Phone);

        return (await signIn.ReadJsonAsync()).GetProperty("access").GetProperty("accessToken").GetString()!;
    }

    private async Task<Trainer> AddTrainerAsync(string fullName, string phone)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trainer = Trainer.Create(fullName, phone, new DateOnly(2026, 10, 1)).Value;
        db.Trainers.Add(trainer);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return trainer;
    }

    private async Task StopAsync(Guid trainerId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trainer = await db.Trainers.Include(row => row.Periods).SingleAsync(row => row.Id == trainerId, TestContext.Current.CancellationToken);
        trainer.Stop(new DateOnly(2026, 10, 20)).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>The Owner's own edit of the trainer (this task's handler), with a new phone.</summary>
    private async Task<Gym.Domain.Common.Result<Gym.Application.Trainers.TrainerResponse>> ChangeTrainerPhoneAsync(Guid trainerId, string phone)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var trainer = await db.Trainers.AsNoTracking().SingleAsync(row => row.Id == trainerId, TestContext.Current.CancellationToken);

        return await scope.ServiceProvider.GetRequiredService<UpdateTrainerHandler>().Handle(
            trainerId,
            new UpdateTrainerCommand(trainer.FullName, phone, trainer.Version),
            TestContext.Current.CancellationToken);
    }

    private async Task<Member> AddMemberAsync(string phone)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = TestMembers.Seed("علی کریمی", phone);
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    /// <summary>The desk's own edit of the member, with a new phone.</summary>
    private async Task ChangeMemberPhoneAsync(Guid memberId, string phone)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Members.AsNoTracking().SingleAsync(row => row.Id == memberId, TestContext.Current.CancellationToken);
        var result = await scope.ServiceProvider.GetRequiredService<Gym.Application.Members.UpdateMember.UpdateMemberHandler>().Handle(
            memberId,
            new Gym.Application.Members.UpdateMember.UpdateMemberCommand(member.FullName, phone, member.Notes, member.BirthDate, member.Version),
            TestContext.Current.CancellationToken);
        result.IsSuccess.ShouldBeTrue();
    }

    private async Task DeactivateAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Members.SingleAsync(row => row.Id == memberId, TestContext.Current.CancellationToken);
        member.Deactivate();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<Member>> MembersAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Members
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<SiteAccount>> AccountsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SiteAccounts
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
