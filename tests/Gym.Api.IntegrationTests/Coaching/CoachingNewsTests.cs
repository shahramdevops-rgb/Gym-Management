using System.Globalization;
using System.Net;
using System.Net.Http.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Api.IntegrationTests.SiteAuth;
using Gym.Application.Common;
using Gym.Application.Trainers.StopTrainer;
using Gym.Domain.Coaching;
using Gym.Domain.Members;
using Gym.Domain.Trainers;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Coaching;

/// <summary>
/// What a member and a trainer are told in the app about each other (BUSINESS_RULES.md §15 <i>A
/// member and their trainer</i>, task 15.4d): the member hears of the trainer's answer and of the
/// trainer ending the link, the trainer of a student ending it, until «دیدم», 30 days at most.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CoachingNewsTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string MemberPhone = "+989121234567";
    private const string TrainerPhone = "+989351234567";
    private const string OtherTrainerPhone = "+989361234567";

    private const string MyNewsPath = "/api/site/coaching/my-trainer/news";
    private const string SeeMyNewsPath = "/api/site/coaching/my-trainer/news/seen";
    private const string StudentNewsPath = "/api/site/coaching/students/news";
    private const string SeeStudentNewsPath = "/api/site/coaching/students/news/seen";

    // ---- The member's «خبر مربی» ----

    [Fact]
    public async Task MyNews_Nothing_IsEmpty()
    {
        await AddMemberAsync(MemberPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        (await NewsAsync(client, token, MyNewsPath)).ShouldBeEmpty();
    }

    [Fact]
    public async Task MyNews_TrainerAccepted_TellsTheMemberWithTheTrainersName()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var requestId = await RequestIdAsync(client, memberToken, trainer.Id);

        await AnswerAsync(client, trainerToken, requestId, "accept");

        var item = (await NewsAsync(client, memberToken, MyNewsPath)).ShouldHaveSingleItem();
        item.Id.ShouldBe(requestId);
        item.Kind.ShouldBe("Accepted");
        item.FullName.ShouldBe("رضا محمدی");
        item.On.ShouldBe(Today());
    }

    [Fact]
    public async Task MyNews_TrainerDeclined_TellsTheMember()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var requestId = await RequestIdAsync(client, memberToken, trainer.Id);

        await AnswerAsync(client, trainerToken, requestId, "decline");

        var item = (await NewsAsync(client, memberToken, MyNewsPath)).ShouldHaveSingleItem();
        item.Id.ShouldBe(requestId);
        item.Kind.ShouldBe("Declined");
    }

    [Fact]
    public async Task MyNews_TrainerEndedTheLink_TellsTheEndAndDropsTheAcceptance()
    {
        var member = await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var requestId = await RequestIdAsync(client, memberToken, trainer.Id);
        await AnswerAsync(client, trainerToken, requestId, "accept");

        using var end = await client.SendAsync(Post($"/api/site/coaching/students/{member.Id}/end", trainerToken), TestContext.Current.CancellationToken);
        end.StatusCode.ShouldBe(HttpStatusCode.OK);

        var item = (await NewsAsync(client, memberToken, MyNewsPath)).ShouldHaveSingleItem();
        item.Kind.ShouldBe("EndedByTrainer");
        item.Id.ShouldBe((await LinksAsync()).ShouldHaveSingleItem().Id);
        item.FullName.ShouldBe("رضا محمدی");
    }

    [Fact]
    public async Task MyNews_MemberEndedTheLinkThemself_TellsTheMemberNothing()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        await AnswerAsync(client, trainerToken, await RequestIdAsync(client, memberToken, trainer.Id), "accept");

        using var end = await client.SendAsync(Post("/api/site/coaching/my-trainer/end", memberToken), TestContext.Current.CancellationToken);
        end.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await NewsAsync(client, memberToken, MyNewsPath)).ShouldBeEmpty();
    }

    [Fact]
    public async Task MyNews_OwnerStoppedTheTrainer_TellsNobody()
    {
        // Decided by the developer, 1405/08/19: the students of a stopped trainer are not told.
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        await AnswerAsync(client, trainerToken, await RequestIdAsync(client, memberToken, trainer.Id), "accept");

        await StopAsync(trainer.Id);

        (await NewsAsync(client, memberToken, MyNewsPath)).ShouldBeEmpty();
    }

    [Fact]
    public async Task MyNews_AnotherTrainerTookTheMemberOver_TellsTheFormerTrainerNothing()
    {
        // Decided by the developer, 1405/08/19: the former trainer is not told.
        await AddMemberAsync(MemberPhone);
        var reza = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        var ali = await AddTrainerAsync("علی کریمی", OtherTrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var rezaToken = await SignInAsync(client, TrainerPhone);
        var aliToken = await SignInAsync(client, OtherTrainerPhone);
        await AnswerAsync(client, rezaToken, await RequestIdAsync(client, memberToken, reza.Id), "accept");

        await AnswerAsync(client, aliToken, await RequestIdAsync(client, memberToken, ali.Id), "accept");

        (await NewsAsync(client, rezaToken, StudentNewsPath)).ShouldBeEmpty();
        var mine = await NewsAsync(client, memberToken, MyNewsPath);
        mine.Select(item => item.FullName).ShouldBe(["علی کریمی"]);
    }

    [Fact]
    public async Task MyNews_AnAnswerThirtyDaysOld_IsNoLongerShown()
    {
        var member = await AddMemberAsync(MemberPhone);
        var reza = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        var ali = await AddTrainerAsync("علی کریمی", OtherTrainerPhone);
        var today = Today();
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var old = TrainerRequest.Create(member.Id, reza.Id, today.AddDays(-31), 0).Value;
            old.Decline(today.AddDays(-CoachingNews.ShownForDays)).IsSuccess.ShouldBeTrue();
            var last = TrainerRequest.Create(member.Id, ali.Id, today.AddDays(-31), 1).Value;
            last.Decline(today.AddDays(-(CoachingNews.ShownForDays - 1))).IsSuccess.ShouldBeTrue();
            db.TrainerRequests.AddRange(old, last);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        (await NewsAsync(client, token, MyNewsPath)).ShouldHaveSingleItem().FullName.ShouldBe("علی کریمی");
    }

    [Fact]
    public async Task SeeMyNews_TheItemsShown_LeaveTheCardAndOthersStay()
    {
        await AddMemberAsync(MemberPhone);
        var reza = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        var ali = await AddTrainerAsync("علی کریمی", OtherTrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var rezaToken = await SignInAsync(client, TrainerPhone);
        var aliToken = await SignInAsync(client, OtherTrainerPhone);
        var declined = await RequestIdAsync(client, memberToken, reza.Id);
        await AnswerAsync(client, rezaToken, declined, "decline");
        var shown = (await NewsAsync(client, memberToken, MyNewsPath)).Select(item => item.Id).ToList();

        // Answered after the card was read: not among what the member saw.
        var accepted = await RequestIdAsync(client, memberToken, ali.Id);
        await AnswerAsync(client, aliToken, accepted, "accept");
        using var seen = await SeeAsync(client, memberToken, SeeMyNewsPath, shown);

        seen.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadNewsAsync(seen)).Select(item => item.Id).ShouldBe([accepted]);
        var requests = await RequestsAsync();
        requests.Single(request => request.Id == declined).AnswerSeenAt.ShouldNotBeNull();
        requests.Single(request => request.Id == accepted).AnswerSeenAt.ShouldBeNull();
        (await NewsAsync(client, memberToken, MyNewsPath)).Select(item => item.Id).ShouldBe([accepted]);
    }

    [Fact]
    public async Task SeeMyNews_Twice_KeepsTheFirstMoment()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var requestId = await RequestIdAsync(client, memberToken, trainer.Id);
        await AnswerAsync(client, trainerToken, requestId, "decline");
        using var first = await SeeAsync(client, memberToken, SeeMyNewsPath, [requestId]);
        var firstMoment = (await RequestsAsync()).ShouldHaveSingleItem().AnswerSeenAt;

        using var second = await SeeAsync(client, memberToken, SeeMyNewsPath, [requestId]);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RequestsAsync()).ShouldHaveSingleItem().AnswerSeenAt.ShouldBe(firstMoment);
    }

    [Fact]
    public async Task SeeMyNews_AnotherMembersAnswer_IsPassedOver()
    {
        await AddMemberAsync(MemberPhone);
        await AddMemberAsync("+989129876543");
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var otherToken = await SignInAsync(client, "+989129876543");
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var requestId = await RequestIdAsync(client, memberToken, trainer.Id);
        await AnswerAsync(client, trainerToken, requestId, "decline");

        using var seen = await SeeAsync(client, otherToken, SeeMyNewsPath, [requestId]);

        seen.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RequestsAsync()).ShouldHaveSingleItem().AnswerSeenAt.ShouldBeNull();
        (await NewsAsync(client, memberToken, MyNewsPath)).ShouldHaveSingleItem();
    }

    // ---- The trainer's «خبر شاگردان» ----

    [Fact]
    public async Task StudentNews_StudentEndedTheLink_TellsTheTrainerWithTheStudentsName()
    {
        await AddMemberAsync(MemberPhone, "مریم احمدی");
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        await AnswerAsync(client, trainerToken, await RequestIdAsync(client, memberToken, trainer.Id), "accept");

        using var end = await client.SendAsync(Post("/api/site/coaching/my-trainer/end", memberToken), TestContext.Current.CancellationToken);
        end.StatusCode.ShouldBe(HttpStatusCode.OK);

        var item = (await NewsAsync(client, trainerToken, StudentNewsPath)).ShouldHaveSingleItem();
        item.Kind.ShouldBe("EndedByStudent");
        item.FullName.ShouldBe("مریم احمدی");
        item.Id.ShouldBe((await LinksAsync()).ShouldHaveSingleItem().Id);
        item.On.ShouldBe(Today());
    }

    [Fact]
    public async Task StudentNews_TrainerEndedTheLinkThemself_TellsTheTrainerNothing()
    {
        var member = await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        await AnswerAsync(client, trainerToken, await RequestIdAsync(client, memberToken, trainer.Id), "accept");

        using var end = await client.SendAsync(Post($"/api/site/coaching/students/{member.Id}/end", trainerToken), TestContext.Current.CancellationToken);
        end.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await NewsAsync(client, trainerToken, StudentNewsPath)).ShouldBeEmpty();
    }

    [Fact]
    public async Task SeeStudentNews_TheEndShown_LeavesTheCard()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        await AnswerAsync(client, trainerToken, await RequestIdAsync(client, memberToken, trainer.Id), "accept");
        using var end = await client.SendAsync(Post("/api/site/coaching/my-trainer/end", memberToken), TestContext.Current.CancellationToken);
        end.StatusCode.ShouldBe(HttpStatusCode.OK);
        var shown = (await NewsAsync(client, trainerToken, StudentNewsPath)).Select(item => item.Id).ToList();

        using var seen = await SeeAsync(client, trainerToken, SeeStudentNewsPath, shown);

        seen.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ReadNewsAsync(seen)).ShouldBeEmpty();
        (await LinksAsync()).ShouldHaveSingleItem().EndSeenAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SeeStudentNews_AnotherTrainersStudent_IsPassedOver()
    {
        await AddMemberAsync(MemberPhone);
        var reza = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        await AddTrainerAsync("علی کریمی", OtherTrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var rezaToken = await SignInAsync(client, TrainerPhone);
        var aliToken = await SignInAsync(client, OtherTrainerPhone);
        await AnswerAsync(client, rezaToken, await RequestIdAsync(client, memberToken, reza.Id), "accept");
        using var end = await client.SendAsync(Post("/api/site/coaching/my-trainer/end", memberToken), TestContext.Current.CancellationToken);
        end.StatusCode.ShouldBe(HttpStatusCode.OK);
        var linkId = (await LinksAsync()).ShouldHaveSingleItem().Id;

        using var seen = await SeeAsync(client, aliToken, SeeStudentNewsPath, [linkId]);

        seen.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await LinksAsync()).ShouldHaveSingleItem().EndSeenAt.ShouldBeNull();
    }

    // ---- Each side's area only ----

    [Fact]
    public async Task StudentNews_AMemberWhoIsNoTrainer_IsRefused()
    {
        await AddMemberAsync(MemberPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        using var response = await client.SendAsync(Get(StudentNewsPath, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MyNews_ATrainerWhoIsNoMember_IsRefused()
    {
        await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, TrainerPhone);

        using var response = await client.SendAsync(Get(MyNewsPath, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- The database holds the rules too ----

    [Fact]
    public async Task Database_AnAnswerDayOnARequestTheTrainerNeverAnswered_IsRefused()
    {
        var member = await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TrainerRequests.Add(TrainerRequest.Create(member.Id, trainer.Id, Today(), 0).Value);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var refused = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE trainer_requests SET answered_on = CURRENT_DATE",
            TestContext.Current.CancellationToken));

        refused.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        refused.ConstraintName.ShouldBe("ck_trainer_requests_answered_on_with_answer");
    }

    [Fact]
    public async Task Database_AnEndSeenOnAnOpenLink_IsRefused()
    {
        var member = await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var request = TrainerRequest.Create(member.Id, trainer.Id, Today(), 0).Value;
        db.TrainerRequests.Add(request);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.TrainerLinks.Add(request.Accept(Today()).Value);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var refused = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(
            "UPDATE trainer_links SET end_seen_at = now()",
            TestContext.Current.CancellationToken));

        refused.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        refused.ConstraintName.ShouldBe("ck_trainer_links_end_seen_at_with_ended_on");
    }

    // ---- Helpers ----

    private sealed record NewsItem(Guid Id, string Kind, string FullName, DateOnly On);

    private static HttpRequestMessage Get(string path, string token) => new HttpRequestMessage(HttpMethod.Get, path).WithBearer(token);

    private static HttpRequestMessage Post(string path, string token) => new HttpRequestMessage(HttpMethod.Post, path).WithBearer(token);

    private static async Task<List<NewsItem>> NewsAsync(HttpClient client, string token, string path)
    {
        using var response = await client.SendAsync(Get(path, token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await ReadNewsAsync(response);
    }

    private static async Task<List<NewsItem>> ReadNewsAsync(HttpResponseMessage response) =>
        (await response.ReadJsonAsync()).GetProperty("items").EnumerateArray()
            .Select(item => new NewsItem(
                item.GetProperty("id").GetGuid(),
                item.GetProperty("kind").GetString()!,
                item.GetProperty("fullName").GetString()!,
                DateOnly.Parse(item.GetProperty("on").GetString()!, CultureInfo.InvariantCulture)))
            .ToList();

    private static Task<HttpResponseMessage> SeeAsync(HttpClient client, string token, string path, IReadOnlyList<Guid> ids)
    {
        var request = Post(path, token);
        request.Content = JsonContent.Create(new { ids });

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>The member asks the trainer, and the id of the request waiting.</summary>
    private static async Task<Guid> RequestIdAsync(HttpClient client, string token, Guid trainerId)
    {
        var request = Post("/api/site/coaching/my-trainer/request", token);
        request.Content = JsonContent.Create(new { trainerId });
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.ReadJsonAsync()).GetProperty("request").GetProperty("requestId").GetGuid();
    }

    /// <param name="answer"><c>accept</c> or <c>decline</c>.</param>
    private static async Task AnswerAsync(HttpClient client, string token, Guid requestId, string answer)
    {
        using var response = await client.SendAsync(Post($"/api/site/coaching/requests/{requestId}/{answer}", token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<string> SignInAsync(HttpClient client, string phone)
    {
        using var signIn = await client.SignInWithCodeAsync(Fixture, phone);
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await signIn.ReadJsonAsync()).GetProperty("access").GetProperty("accessToken").GetString()!;
    }

    private DateOnly Today()
    {
        using var scope = Fixture.CreateScope();

        return scope.ServiceProvider.GetRequiredService<IGymCalendar>().Today();
    }

    private async Task<Member> AddMemberAsync(string phone, string fullName = "علی کریمی")
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = TestMembers.Seed(fullName, phone);
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
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

    /// <summary>The Owner's «توقف» in the panel, through its own handler.</summary>
    private async Task StopAsync(Guid trainerId)
    {
        await using var scope = Fixture.CreateScope();
        var stopped = await scope.ServiceProvider.GetRequiredService<StopTrainerHandler>().Handle(trainerId, TestContext.Current.CancellationToken);
        stopped.IsSuccess.ShouldBeTrue();
    }

    private async Task<List<TrainerRequest>> RequestsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TrainerRequests.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<TrainerLink>> LinksAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().TrainerLinks.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }
}
