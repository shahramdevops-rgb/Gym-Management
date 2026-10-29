using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Api.IntegrationTests.SiteAuth;
using Gym.Application.Coaching;
using Gym.Application.Common;
using Gym.Application.Trainers.StopTrainer;
using Gym.Domain.Coaching;
using Gym.Domain.Members;
using Gym.Domain.Trainers;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Api.IntegrationTests.Coaching;

/// <summary>
/// A member and their trainer on the site (BUSINESS_RULES.md §15 <i>A member and their trainer</i>,
/// task 15.4): the member picks, the trainer accepts or declines, one trainer at a time, and a
/// former trainer stops seeing the member the day the link ends.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class CoachingTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string MemberPhone = "+989121234567";
    private const string TrainerPhone = "+989351234567";
    private const string OtherTrainerPhone = "+989361234567";

    private const string TrainersPath = "/api/site/coaching/trainers";
    private const string MyTrainerPath = "/api/site/coaching/my-trainer";
    private const string RequestPath = "/api/site/coaching/my-trainer/request";
    private const string CancelRequestPath = "/api/site/coaching/my-trainer/cancel-request";
    private const string EndMyTrainerPath = "/api/site/coaching/my-trainer/end";
    private const string StudentsPath = "/api/site/coaching/students";

    // ---- The member's list of trainers ----

    [Fact]
    public async Task ListTrainers_TrainersNow_ByNameWithoutStoppedOnesOrTheMemberThemself()
    {
        await AddMemberAsync(MemberPhone);
        var reza = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        var ali = await AddTrainerAsync("علی کریمی", OtherTrainerPhone);
        var stopped = await AddTrainerAsync("بهرام نوری", "+989371234567");
        await StopAsync(stopped.Id);
        await AddTrainerAsync("خود عضو", MemberPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        using var response = await client.SendAsync(Get(TrainersPath, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var trainers = (await response.ReadJsonAsync()).EnumerateArray()
            .Select(trainer => trainer.GetProperty("trainerId").GetGuid())
            .ToList();
        trainers.ShouldBe([reza.Id, ali.Id]);
    }

    // ---- The member's request ----

    [Fact]
    public async Task Request_ATrainerNow_WaitsForTheirAnswer()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        using var response = await RequestAsync(client, token, trainer.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("trainer").ValueKind.ShouldBe(JsonValueKind.Null);
        var request = body.GetProperty("request");
        request.GetProperty("trainerId").GetGuid().ShouldBe(trainer.Id);
        request.GetProperty("fullName").GetString().ShouldBe("رضا محمدی");
        request.GetProperty("requestedOn").GetString().ShouldBe(Today().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

        using var mine = await client.SendAsync(Get(MyTrainerPath, token), TestContext.Current.CancellationToken);
        (await mine.ReadJsonAsync()).GetProperty("request").GetProperty("trainerId").GetGuid().ShouldBe(trainer.Id);
    }

    [Fact]
    public async Task Request_TheSameTrainerTwice_MakesOneRequest()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        using var first = await RequestAsync(client, token, trainer.Id);
        using var second = await RequestAsync(client, token, trainer.Id);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RequestsAsync()).ShouldHaveSingleItem().Status.ShouldBe(TrainerRequestStatus.Pending);
    }

    [Fact]
    public async Task Request_AnotherTrainerWhileOneWaits_ReplacesTheWaitingOne()
    {
        await AddMemberAsync(MemberPhone);
        var reza = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        var ali = await AddTrainerAsync("علی کریمی", OtherTrainerPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);
        using var first = await RequestAsync(client, token, reza.Id);

        using var second = await RequestAsync(client, token, ali.Id);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.ReadJsonAsync()).GetProperty("request").GetProperty("trainerId").GetGuid().ShouldBe(ali.Id);
        var requests = await RequestsAsync();
        requests.Single(request => request.TrainerId == reza.Id).Status.ShouldBe(TrainerRequestStatus.Replaced);
        requests.Single(request => request.TrainerId == ali.Id).Status.ShouldBe(TrainerRequestStatus.Pending);
    }

    [Fact]
    public async Task Request_AStoppedTrainer_Returns404()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        await StopAsync(trainer.Id);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        using var response = await RequestAsync(client, token, trainer.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.TrainerNotFound.Code);
        (await RequestsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Request_ThemselfAsTrainer_Returns422()
    {
        // One person, member and trainer with one phone.
        await AddMemberAsync(MemberPhone);
        var self = await AddTrainerAsync("علی کریمی", MemberPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        using var response = await RequestAsync(client, token, self.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.CannotPickYourself.Code);
    }

    [Fact]
    public async Task Request_TheTrainerTheyHave_Returns409()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        await AcceptAsync(client, trainerToken, await RequestIdAsync(client, memberToken, trainer.Id));

        using var response = await RequestAsync(client, memberToken, trainer.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.AlreadyYourTrainer.Code);
    }

    [Fact]
    public async Task Request_MoreThanTheDaysAllowance_Returns422()
    {
        await AddMemberAsync(MemberPhone);
        var reza = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        var ali = await AddTrainerAsync("علی کریمی", OtherTrainerPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);
        for (var made = 0; made < TrainerRequest.MaxPerDay; made++)
        {
            using var allowed = await RequestAsync(client, token, made % 2 == 0 ? reza.Id : ali.Id);
            allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // The last one went to Reza, so this is a new request, not a second press.
        using var response = await RequestAsync(client, token, ali.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.TooManyRequestsToday.Code);
        (await RequestsAsync()).Count.ShouldBe(TrainerRequest.MaxPerDay);
    }

    [Fact]
    public async Task Request_MemberDeactivatedAfterSigningIn_Returns403()
    {
        // The token still says member for up to 15 minutes; the handler reads the desk's now.
        var member = await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);
        await DeactivateAsync(member.Id);

        using var response = await RequestAsync(client, token, trainer.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.MemberInactive.Code);
    }

    [Fact]
    public async Task CancelRequest_TheWaitingOne_LeavesNoRequest()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);
        using var requested = await RequestAsync(client, token, trainer.Id);

        using var response = await client.SendAsync(Post(CancelRequestPath, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("request").ValueKind.ShouldBe(JsonValueKind.Null);
        (await RequestsAsync()).ShouldHaveSingleItem().Status.ShouldBe(TrainerRequestStatus.Cancelled);
    }

    [Fact]
    public async Task CancelRequest_NoneWaiting_Returns409()
    {
        await AddMemberAsync(MemberPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        using var response = await client.SendAsync(Post(CancelRequestPath, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.NoPendingRequest.Code);
    }

    // ---- The trainer's answer ----

    [Fact]
    public async Task Accept_TheRequest_MakesTheMemberTheirStudentToday()
    {
        var member = await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var requestId = await RequestIdAsync(client, memberToken, trainer.Id);

        using var waiting = await client.SendAsync(Get(StudentsPath, trainerToken), TestContext.Current.CancellationToken);
        var request = (await waiting.ReadJsonAsync()).GetProperty("requests").EnumerateArray().ShouldHaveSingleItem();
        request.GetProperty("requestId").GetGuid().ShouldBe(requestId);
        request.GetProperty("fullName").GetString().ShouldBe("علی کریمی");
        request.GetProperty("phoneNumber").GetString().ShouldBe(MemberPhone);

        using var response = await client.SendAsync(Post($"/api/site/coaching/requests/{requestId}/accept", trainerToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.ReadJsonAsync();
        body.GetProperty("requests").GetArrayLength().ShouldBe(0);
        var student = body.GetProperty("students").EnumerateArray().ShouldHaveSingleItem();
        student.GetProperty("memberId").GetGuid().ShouldBe(member.Id);
        student.GetProperty("phoneNumber").GetString().ShouldBe(MemberPhone);
        student.GetProperty("since").GetString().ShouldBe(Today().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        student.GetProperty("isActive").GetBoolean().ShouldBeTrue();

        using var mine = await client.SendAsync(Get(MyTrainerPath, memberToken), TestContext.Current.CancellationToken);
        var mineBody = await mine.ReadJsonAsync();
        mineBody.GetProperty("trainer").GetProperty("trainerId").GetGuid().ShouldBe(trainer.Id);
        mineBody.GetProperty("request").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Accept_ChangingTrainer_TheFormerTrainerLosesTheMemberThatDay()
    {
        var member = await AddMemberAsync(MemberPhone);
        var reza = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        var ali = await AddTrainerAsync("علی کریمی", OtherTrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var rezaToken = await SignInAsync(client, TrainerPhone);
        var aliToken = await SignInAsync(client, OtherTrainerPhone);
        await AcceptAsync(client, rezaToken, await RequestIdAsync(client, memberToken, reza.Id));

        // While the request to Ali waits, Reza is still the member's trainer.
        var toAli = await RequestIdAsync(client, memberToken, ali.Id);
        using var meanwhile = await client.SendAsync(Get(MyTrainerPath, memberToken), TestContext.Current.CancellationToken);
        (await meanwhile.ReadJsonAsync()).GetProperty("trainer").GetProperty("trainerId").GetGuid().ShouldBe(reza.Id);

        await AcceptAsync(client, aliToken, toAli);

        using var rezaStudents = await client.SendAsync(Get(StudentsPath, rezaToken), TestContext.Current.CancellationToken);
        (await rezaStudents.ReadJsonAsync()).GetProperty("students").GetArrayLength().ShouldBe(0);

        var links = await LinksAsync();
        links.Count.ShouldBe(2);
        var former = links.Single(link => link.TrainerId == reza.Id);
        former.EndedOn.ShouldBe(Today());
        former.EndReason.ShouldBe(TrainerLinkEndReason.NewTrainer);
        var open = links.Where(link => link.MemberId == member.Id && link.IsOpen).ShouldHaveSingleItem();
        open.TrainerId.ShouldBe(ali.Id);
    }

    [Fact]
    public async Task Accept_AnotherTrainersRequest_Returns404()
    {
        await AddMemberAsync(MemberPhone);
        var reza = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        await AddTrainerAsync("علی کریمی", OtherTrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var aliToken = await SignInAsync(client, OtherTrainerPhone);
        var toReza = await RequestIdAsync(client, memberToken, reza.Id);

        using var response = await client.SendAsync(Post($"/api/site/coaching/requests/{toReza}/accept", aliToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.RequestNotFound.Code);
        (await LinksAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Accept_ARequestTheMemberTookBack_Returns409()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var requestId = await RequestIdAsync(client, memberToken, trainer.Id);
        using var cancelled = await client.SendAsync(Post(CancelRequestPath, memberToken), TestContext.Current.CancellationToken);

        using var response = await client.SendAsync(Post($"/api/site/coaching/requests/{requestId}/accept", trainerToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.RequestNoLongerPending.Code);
        (await LinksAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Accept_AndTheMemberTakingItBackAtOnce_OneOfTheTwoHappens()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var requestId = await RequestIdAsync(client, memberToken, trainer.Id);

        var accept = client.SendAsync(Post($"/api/site/coaching/requests/{requestId}/accept", trainerToken), TestContext.Current.CancellationToken);
        var cancel = client.SendAsync(Post(CancelRequestPath, memberToken), TestContext.Current.CancellationToken);
        using var accepted = await accept;
        using var cancelled = await cancel;

        new[] { accepted.StatusCode, cancelled.StatusCode }.ShouldBe([HttpStatusCode.OK, HttpStatusCode.Conflict], ignoreOrder: true);
        var request = (await RequestsAsync()).ShouldHaveSingleItem();
        if (accepted.StatusCode == HttpStatusCode.OK)
        {
            request.Status.ShouldBe(TrainerRequestStatus.Accepted);
            (await LinksAsync()).ShouldHaveSingleItem().IsOpen.ShouldBeTrue();
        }
        else
        {
            request.Status.ShouldBe(TrainerRequestStatus.Cancelled);
            (await LinksAsync()).ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task Decline_TheRequest_LeavesTheMemberFreeToAskAgain()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var requestId = await RequestIdAsync(client, memberToken, trainer.Id);

        using var response = await client.SendAsync(Post($"/api/site/coaching/requests/{requestId}/decline", trainerToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("requests").GetArrayLength().ShouldBe(0);
        (await LinksAsync()).ShouldBeEmpty();

        using var again = await RequestAsync(client, memberToken, trainer.Id);
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RequestsAsync()).Select(request => request.Status)
            .ShouldBe([TrainerRequestStatus.Declined, TrainerRequestStatus.Pending], ignoreOrder: true);
    }

    [Fact]
    public async Task Students_ARequestFromAMemberTheDeskDeactivated_IsLeftOutAndCannotBeAccepted()
    {
        var member = await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        var requestId = await RequestIdAsync(client, memberToken, trainer.Id);
        await DeactivateAsync(member.Id);

        using var students = await client.SendAsync(Get(StudentsPath, trainerToken), TestContext.Current.CancellationToken);
        using var accept = await client.SendAsync(Post($"/api/site/coaching/requests/{requestId}/accept", trainerToken), TestContext.Current.CancellationToken);

        (await students.ReadJsonAsync()).GetProperty("requests").GetArrayLength().ShouldBe(0);
        accept.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RequestsAsync()).ShouldHaveSingleItem().Status.ShouldBe(TrainerRequestStatus.Pending);
    }

    [Fact]
    public async Task Students_AStudentTheDeskDeactivated_StaysMarkedInactive()
    {
        var member = await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        await AcceptAsync(client, trainerToken, await RequestIdAsync(client, memberToken, trainer.Id));
        await DeactivateAsync(member.Id);

        using var response = await client.SendAsync(Get(StudentsPath, trainerToken), TestContext.Current.CancellationToken);

        var student = (await response.ReadJsonAsync()).GetProperty("students").EnumerateArray().ShouldHaveSingleItem();
        student.GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }

    // ---- Ending the link ----

    [Fact]
    public async Task EndMyTrainer_TheMemberEndsIt_EndsTodayAndTheTrainerNoLongerSeesThem()
    {
        await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        await AcceptAsync(client, trainerToken, await RequestIdAsync(client, memberToken, trainer.Id));

        using var response = await client.SendAsync(Post(EndMyTrainerPath, memberToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("trainer").ValueKind.ShouldBe(JsonValueKind.Null);
        using var students = await client.SendAsync(Get(StudentsPath, trainerToken), TestContext.Current.CancellationToken);
        (await students.ReadJsonAsync()).GetProperty("students").GetArrayLength().ShouldBe(0);
        var link = (await LinksAsync()).ShouldHaveSingleItem();
        link.EndedOn.ShouldBe(Today());
        link.EndReason.ShouldBe(TrainerLinkEndReason.ByMember);
    }

    [Fact]
    public async Task EndMyTrainer_NoTrainer_Returns409()
    {
        await AddMemberAsync(MemberPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        using var response = await client.SendAsync(Post(EndMyTrainerPath, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.NoTrainer.Code);
    }

    [Fact]
    public async Task EndStudent_TheTrainerEndsIt_TheMemberHasNoTrainer()
    {
        var member = await AddMemberAsync(MemberPhone);
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var trainerToken = await SignInAsync(client, TrainerPhone);
        await AcceptAsync(client, trainerToken, await RequestIdAsync(client, memberToken, trainer.Id));

        using var response = await client.SendAsync(Post($"/api/site/coaching/students/{member.Id}/end", trainerToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("students").GetArrayLength().ShouldBe(0);
        using var mine = await client.SendAsync(Get(MyTrainerPath, memberToken), TestContext.Current.CancellationToken);
        (await mine.ReadJsonAsync()).GetProperty("trainer").ValueKind.ShouldBe(JsonValueKind.Null);
        (await LinksAsync()).ShouldHaveSingleItem().EndReason.ShouldBe(TrainerLinkEndReason.ByTrainer);
    }

    [Fact]
    public async Task EndStudent_NotTheirStudent_Returns404()
    {
        var member = await AddMemberAsync(MemberPhone);
        await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var trainerToken = await SignInAsync(client, TrainerPhone);

        using var response = await client.SendAsync(Post($"/api/site/coaching/students/{member.Id}/end", trainerToken), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.StudentNotFound.Code);
    }

    // ---- The Owner stops the trainer ----

    [Fact]
    public async Task StopTrainer_WithStudentsAndARequest_EndsThemAllToday()
    {
        await AddMemberAsync(MemberPhone);
        var waitingMember = await AddMemberAsync("+989131234567");
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var memberToken = await SignInAsync(client, MemberPhone);
        var waitingToken = await SignInAsync(client, "+989131234567");
        var trainerToken = await SignInAsync(client, TrainerPhone);
        await AcceptAsync(client, trainerToken, await RequestIdAsync(client, memberToken, trainer.Id));
        await RequestIdAsync(client, waitingToken, trainer.Id);

        await StopAsync(trainer.Id);

        var link = (await LinksAsync()).ShouldHaveSingleItem();
        link.EndedOn.ShouldBe(Today());
        link.EndReason.ShouldBe(TrainerLinkEndReason.TrainerStopped);
        (await RequestsAsync()).Single(request => request.MemberId == waitingMember.Id).Status.ShouldBe(TrainerRequestStatus.TrainerStopped);
        using var mine = await client.SendAsync(Get(MyTrainerPath, memberToken), TestContext.Current.CancellationToken);
        (await mine.ReadJsonAsync()).GetProperty("trainer").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Students_ATrainerStoppedAfterSigningIn_Returns403()
    {
        // The token still says trainer for up to 15 minutes; the handler reads the Owner's now.
        var trainer = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, TrainerPhone);
        await StopAsync(trainer.Id);

        using var response = await client.SendAsync(Get(StudentsPath, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.NotATrainer.Code);
    }

    // ---- The two areas ----

    [Fact]
    public async Task TrainersArea_WithAMembersToken_Returns403()
    {
        await AddMemberAsync(MemberPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, MemberPhone);

        using var response = await client.SendAsync(Get(StudentsPath, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MembersArea_WithATrainersToken_Returns403()
    {
        await AddTrainerAsync("رضا محمدی", TrainerPhone);
        using var client = Fixture.CreateClient();
        var token = await SignInAsync(client, TrainerPhone);

        using var response = await client.SendAsync(Get(MyTrainerPath, token), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- The database holds the rules too ----

    [Fact]
    public async Task Database_TwoWaitingRequestsForOneMember_IsRefused()
    {
        var member = await AddMemberAsync(MemberPhone);
        var reza = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        var ali = await AddTrainerAsync("علی کریمی", OtherTrainerPhone);
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.TrainerRequests.Add(TrainerRequest.Create(member.Id, reza.Id, Today(), 0).Value);
        db.TrainerRequests.Add(TrainerRequest.Create(member.Id, ali.Id, Today(), 1).Value);

        var refused = await Should.ThrowAsync<UniqueConstraintException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        refused.ConstraintName.ShouldBe(CoachingConstraints.OnePendingRequest);
    }

    [Fact]
    public async Task Database_TwoOpenLinksForOneMember_IsRefused()
    {
        var member = await AddMemberAsync(MemberPhone);
        var reza = await AddTrainerAsync("رضا محمدی", TrainerPhone);
        var ali = await AddTrainerAsync("علی کریمی", OtherTrainerPhone);
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var toReza = TrainerRequest.Create(member.Id, reza.Id, Today(), 0).Value;
        var toAli = TrainerRequest.Create(member.Id, ali.Id, Today(), 1).Value;
        db.TrainerRequests.Add(toReza);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.TrainerLinks.Add(toReza.Accept(Today()).Value);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.TrainerRequests.Add(toAli);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.TrainerLinks.Add(toAli.Accept(Today()).Value);

        var refused = await Should.ThrowAsync<UniqueConstraintException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        refused.ConstraintName.ShouldBe(CoachingConstraints.OneOpenLink);
    }

    // ---- Helpers ----

    private static HttpRequestMessage Get(string path, string token) => new HttpRequestMessage(HttpMethod.Get, path).WithBearer(token);

    private static HttpRequestMessage Post(string path, string token) => new HttpRequestMessage(HttpMethod.Post, path).WithBearer(token);

    private static Task<HttpResponseMessage> RequestAsync(HttpClient client, string token, Guid trainerId)
    {
        var request = Post(RequestPath, token);
        request.Content = JsonContent.Create(new { trainerId });

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>The member asks the trainer, and the id of the request waiting.</summary>
    private static async Task<Guid> RequestIdAsync(HttpClient client, string token, Guid trainerId)
    {
        using var response = await RequestAsync(client, token, trainerId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await response.ReadJsonAsync()).GetProperty("request").GetProperty("requestId").GetGuid();
    }

    private static async Task AcceptAsync(HttpClient client, string token, Guid requestId)
    {
        using var response = await client.SendAsync(Post($"/api/site/coaching/requests/{requestId}/accept", token), TestContext.Current.CancellationToken);
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

    private async Task<Member> AddMemberAsync(string phone)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = TestMembers.Seed("علی کریمی", phone);
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

    private async Task DeactivateAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Members.SingleAsync(row => row.Id == memberId, TestContext.Current.CancellationToken);
        member.Deactivate();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
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
