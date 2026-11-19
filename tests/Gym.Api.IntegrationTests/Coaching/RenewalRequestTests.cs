using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Api.IntegrationTests.SiteAuth;
using Gym.Application.Common;
using Gym.Application.Trainers.StopTrainer;
using Gym.Domain.Audit;
using Gym.Domain.Coaching;
using Gym.Domain.Members;
using Gym.Domain.Trainers;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Coaching;

/// <summary>
/// «درخواست تمدید» (BUSINESS_RULES.md §15 <i>The coaching period</i>, task 15.7o): the member asks
/// from the period's last days or after it is over, the trainer renews or declines in «درخواست‌ها»,
/// a «تمدید» or a later «ویرایش» elsewhere answers it, and ending the coaching closes it.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class RenewalRequestTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string MemberPhone = "+989121234567";
    private const string TrainerPhone = "+989351234567";
    private const string OtherTrainerPhone = "+989361234567";
    private const string StudentsPath = "/api/site/coaching/students";
    private const string MyTrainerPath = "/api/site/coaching/my-trainer";
    private const string RenewalPath = "/api/site/coaching/my-trainer/renewal-request";

    // ---- Asking ----

    [Fact]
    public async Task Request_SevenDaysLeft_WaitsAndBothSidesSeeIt()
    {
        var gym = await LinkedGymAsync(days: 7);

        using var asked = await AskAsync(gym);

        asked.StatusCode.ShouldBe(HttpStatusCode.OK);
        var trainer = (await asked.ReadJsonAsync()).GetProperty("trainer");
        trainer.GetProperty("renewal").GetString().ShouldBe("Pending");
        trainer.GetProperty("canAskRenewal").GetBoolean().ShouldBeFalse();
        var renewal = (await GetJsonAsync(gym.Client, StudentsPath, gym.TrainerToken)).GetProperty("renewalRequests")
            .EnumerateArray().ShouldHaveSingleItem();
        renewal.GetProperty("memberId").GetGuid().ShouldBe(gym.MemberId);
        renewal.GetProperty("fullName").GetString().ShouldBe("علی کریمی");
        renewal.GetProperty("phoneNumber").GetString().ShouldBe(MemberPhone);
        renewal.GetProperty("requestedOn").GetString().ShouldBe(Iso(Today()));
        renewal.GetProperty("periodEndsOn").GetString().ShouldBe(Iso(Today().AddDays(6)));
        var request = (await RenewalsAsync()).ShouldHaveSingleItem();
        (request.Status, request.RequestedOn, request.PeriodEndsOn).ShouldBe((RenewalRequestStatus.Pending, Today(), Today().AddDays(6)));
    }

    [Fact]
    public async Task Request_EightDaysLeft_Returns422AndTheButtonIsNotShown()
    {
        var gym = await LinkedGymAsync(days: 8);

        var before = (await GetJsonAsync(gym.Client, MyTrainerPath, gym.MemberToken)).GetProperty("trainer");
        using var asked = await AskAsync(gym);

        before.GetProperty("canAskRenewal").GetBoolean().ShouldBeFalse();
        before.GetProperty("renewal").ValueKind.ShouldBe(JsonValueKind.Null);
        asked.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await asked.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.RenewalNotYet.Code);
        (await RenewalsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Request_TheSettingsNumber_MovesTheWindow()
    {
        var gym = await LinkedGymAsync(days: 10);
        await SetSoonDaysAsync(10);

        var mine = (await GetJsonAsync(gym.Client, MyTrainerPath, gym.MemberToken)).GetProperty("trainer");
        using var asked = await AskAsync(gym);

        mine.GetProperty("canAskRenewal").GetBoolean().ShouldBeTrue();
        asked.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Request_AfterThePeriodIsOver_Waits()
    {
        var gym = await LinkedGymAsync(days: 30);
        await MovePeriodBackAsync(40);

        var mine = (await GetJsonAsync(gym.Client, MyTrainerPath, gym.MemberToken)).GetProperty("trainer");
        using var asked = await AskAsync(gym);

        mine.GetProperty("canAskRenewal").GetBoolean().ShouldBeTrue();
        asked.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RenewalsAsync()).ShouldHaveSingleItem().PeriodEndsOn.ShouldBe(Today().AddDays(-11));
    }

    [Fact]
    public async Task Request_LinkWithoutPeriod_Returns422()
    {
        var gym = await LinkedGymAsync(days: 7);
        await using (var scope = Fixture.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database
                .ExecuteSqlAsync($"DELETE FROM coaching_periods", TestContext.Current.CancellationToken);
        }

        var mine = (await GetJsonAsync(gym.Client, MyTrainerPath, gym.MemberToken)).GetProperty("trainer");
        using var asked = await AskAsync(gym);

        mine.GetProperty("canAskRenewal").GetBoolean().ShouldBeFalse();
        asked.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await asked.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.RenewalNotYet.Code);
    }

    [Fact]
    public async Task Request_NoTrainer_Returns409()
    {
        var gym = await GymAsync();

        using var asked = await AskAsync(gym);

        asked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await asked.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.NoTrainer.Code);
    }

    [Fact]
    public async Task Request_Twice_MakesOneRequest()
    {
        var gym = await LinkedGymAsync(days: 7);
        using (var first = await AskAsync(gym))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var second = await AskAsync(gym);

        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RenewalsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Request_AsTheTrainer_Returns403()
    {
        var gym = await LinkedGymAsync(days: 7);

        using var asked = await gym.Client.SendAsync(Send(HttpMethod.Post, RenewalPath, gym.TrainerToken), TestContext.Current.CancellationToken);

        asked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Request_WrittenToTheAuditLog()
    {
        var gym = await LinkedGymAsync(days: 7);

        using var asked = await AskAsync(gym);

        asked.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var scope = Fixture.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .AnyAsync(row => row.EntityType == nameof(RenewalRequest) && row.Action == AuditAction.Insert, TestContext.Current.CancellationToken))
            .ShouldBeTrue();
    }

    // ---- «لغو درخواست» ----

    [Fact]
    public async Task Cancel_Waiting_IsCancelledAndTheButtonComesBack()
    {
        var gym = await AskedGymAsync(days: 7);

        using var cancelled = await CancelAsync(gym);

        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);
        var trainer = (await cancelled.ReadJsonAsync()).GetProperty("trainer");
        trainer.GetProperty("renewal").ValueKind.ShouldBe(JsonValueKind.Null);
        trainer.GetProperty("canAskRenewal").GetBoolean().ShouldBeTrue();
        (await RenewalsAsync()).ShouldHaveSingleItem().Status.ShouldBe(RenewalRequestStatus.Cancelled);
        (await GetJsonAsync(gym.Client, StudentsPath, gym.TrainerToken)).GetProperty("renewalRequests").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Cancel_NoneWaiting_Returns409()
    {
        var gym = await LinkedGymAsync(days: 7);

        using var cancelled = await CancelAsync(gym);

        cancelled.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await cancelled.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.NoRenewalRequest.Code);
    }

    // ---- «تمدید» from «درخواست‌ها» ----

    [Fact]
    public async Task Accept_DaysTyped_RenewsAfterTheLastDayAndAnswersIt()
    {
        var gym = await AskedGymAsync(days: 7);

        using var accepted = await AcceptAsync(gym, await RenewalIdAsync(), new { days = 30 });

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var students = await accepted.ReadJsonAsync();
        students.GetProperty("renewalRequests").GetArrayLength().ShouldBe(0);
        students.GetProperty("students").EnumerateArray().Single().GetProperty("periodEndsOn").GetString().ShouldBe(Iso(Today().AddDays(36)));
        var request = (await RenewalsAsync()).ShouldHaveSingleItem();
        (request.Status, request.AnsweredOn).ShouldBe((RenewalRequestStatus.Renewed, Today()));
        var mine = (await GetJsonAsync(gym.Client, MyTrainerPath, gym.MemberToken)).GetProperty("trainer");
        mine.GetProperty("renewal").ValueKind.ShouldBe(JsonValueKind.Null);
        mine.GetProperty("canAskRenewal").GetBoolean().ShouldBeFalse();
        mine.GetProperty("periodEndsOn").GetString().ShouldBe(Iso(Today().AddDays(36)));
    }

    [Fact]
    public async Task Accept_AfterThePeriodIsOver_StartsToday()
    {
        var gym = await LinkedGymAsync(days: 30);
        await MovePeriodBackAsync(40);
        using (var asked = await AskAsync(gym))
        {
            asked.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var accepted = await AcceptAsync(gym, await RenewalIdAsync(), new { days = 20 });

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var latest = (await PeriodsAsync()).MaxBy(period => period.EndsOn)!;
        (latest.StartsOn, latest.EndsOn).ShouldBe((Today(), Today().AddDays(19)));
    }

    [Theory]
    [InlineData(CoachingPeriod.MinDays - 1)]
    [InlineData(CoachingPeriod.MaxDays + 1)]
    public async Task Accept_DaysOutOfRange_Returns400AndItStillWaits(int days)
    {
        var gym = await AskedGymAsync(days: 7);

        using var accepted = await AcceptAsync(gym, await RenewalIdAsync(), new { days });

        accepted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await accepted.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.PeriodDaysOutOfRange.Code);
        (await RenewalsAsync()).ShouldHaveSingleItem().IsPending.ShouldBeTrue();
        (await PeriodsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Accept_TakenBackMeanwhile_Returns409AndThePeriodStays()
    {
        var gym = await AskedGymAsync(days: 7);
        var renewalId = await RenewalIdAsync();
        using (var cancelled = await CancelAsync(gym))
        {
            cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var accepted = await AcceptAsync(gym, renewalId, new { days = 30 });

        accepted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await accepted.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.RenewalNoLongerPending.Code);
        (await PeriodsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Accept_AnotherTrainersStudent_Returns404()
    {
        var gym = await AskedGymAsync(days: 7);
        await AddTrainerAsync("مهدی رحیمی", OtherTrainerPhone);
        var otherToken = await SignInAsync(gym.Client, OtherTrainerPhone);

        using var accepted = await AcceptAsync(gym, await RenewalIdAsync(), new { days = 30 }, otherToken);
        using var declined = await DeclineAsync(gym, await RenewalIdAsync(), otherToken);

        accepted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await accepted.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.RenewalRequestNotFound.Code);
        declined.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RenewalsAsync()).ShouldHaveSingleItem().IsPending.ShouldBeTrue();
    }

    // ---- «رد کردن» ----

    [Fact]
    public async Task Decline_Waiting_MemberReadsItAndMayAskAgain()
    {
        var gym = await AskedGymAsync(days: 7);

        using var declined = await DeclineAsync(gym, await RenewalIdAsync());

        declined.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await declined.ReadJsonAsync()).GetProperty("renewalRequests").GetArrayLength().ShouldBe(0);
        var request = (await RenewalsAsync()).ShouldHaveSingleItem();
        (request.Status, request.AnsweredOn).ShouldBe((RenewalRequestStatus.Declined, Today()));
        var mine = (await GetJsonAsync(gym.Client, MyTrainerPath, gym.MemberToken)).GetProperty("trainer");
        mine.GetProperty("renewal").GetString().ShouldBe("Declined");
        mine.GetProperty("canAskRenewal").GetBoolean().ShouldBeTrue();

        using var again = await AskAsync(gym);

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await again.ReadJsonAsync()).GetProperty("trainer").GetProperty("renewal").GetString().ShouldBe("Pending");
        (await RenewalsAsync()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Decline_AskedAgainThenTakenBack_NoLongerShown()
    {
        // Asking again replaces what the decline said, even when taken back.
        var gym = await AskedGymAsync(days: 7);
        using (var declined = await DeclineAsync(gym, await RenewalIdAsync()))
        {
            declined.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using (var again = await AskAsync(gym))
        {
            again.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var cancelled = await CancelAsync(gym);

        (await cancelled.ReadJsonAsync()).GetProperty("trainer").GetProperty("renewal").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Decline_ThenTheLastDayMoves_NoLongerShown()
    {
        var gym = await AskedGymAsync(days: 7);
        using (var declined = await DeclineAsync(gym, await RenewalIdAsync()))
        {
            declined.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var edited = await SetDaysLeftAsync(gym, 5);

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        var mine = (await GetJsonAsync(gym.Client, MyTrainerPath, gym.MemberToken)).GetProperty("trainer");
        mine.GetProperty("renewal").ValueKind.ShouldBe(JsonValueKind.Null);
        mine.GetProperty("canAskRenewal").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Decline_Twice_Returns409()
    {
        var gym = await AskedGymAsync(days: 7);
        var renewalId = await RenewalIdAsync();
        using (var first = await DeclineAsync(gym, renewalId))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var second = await DeclineAsync(gym, renewalId);

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.ReadErrorCodeAsync()).ShouldBe(CoachingErrors.RenewalNoLongerPending.Code);
    }

    // ---- The trainer's other changes ----

    [Fact]
    public async Task RenewOnTheSchedulePage_AnswersTheWaitingRequest()
    {
        var gym = await AskedGymAsync(days: 7);

        using var renewed = await SendJsonAsync(gym, $"{StudentsPath}/{gym.MemberId}/period", new { days = 30 });

        renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var request = (await RenewalsAsync()).ShouldHaveSingleItem();
        (request.Status, request.AnsweredOn).ShouldBe((RenewalRequestStatus.Renewed, Today()));
        (await GetJsonAsync(gym.Client, StudentsPath, gym.TrainerToken)).GetProperty("renewalRequests").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task SetDaysLeft_LaterLastDay_AnswersTheWaitingRequest()
    {
        var gym = await AskedGymAsync(days: 7);

        using var edited = await SetDaysLeftAsync(gym, 20);

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RenewalsAsync()).ShouldHaveSingleItem().Status.ShouldBe(RenewalRequestStatus.Renewed);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public async Task SetDaysLeft_EarlierOrTheSameLastDay_LeavesItWaiting(int daysLeft)
    {
        var gym = await AskedGymAsync(days: 7);

        using var edited = await SetDaysLeftAsync(gym, daysLeft);

        edited.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RenewalsAsync()).ShouldHaveSingleItem().IsPending.ShouldBeTrue();
        var renewal = (await GetJsonAsync(gym.Client, StudentsPath, gym.TrainerToken)).GetProperty("renewalRequests")
            .EnumerateArray().ShouldHaveSingleItem();
        renewal.GetProperty("periodEndsOn").GetString().ShouldBe(Iso(Today().AddDays(daysLeft - 1)));
    }

    // ---- Ending the coaching closes it ----

    [Fact]
    public async Task EndByMember_ClosesTheWaitingRequest()
    {
        var gym = await AskedGymAsync(days: 7);

        using var ended = await gym.Client.SendAsync(Send(HttpMethod.Post, $"{MyTrainerPath}/end", gym.MemberToken), TestContext.Current.CancellationToken);

        ended.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RenewalsAsync()).ShouldHaveSingleItem().Status.ShouldBe(RenewalRequestStatus.LinkEnded);
    }

    [Fact]
    public async Task EndByTrainer_ClosesTheWaitingRequest()
    {
        var gym = await AskedGymAsync(days: 7);

        using var ended = await gym.Client.SendAsync(
            Send(HttpMethod.Post, $"{StudentsPath}/{gym.MemberId}/end", gym.TrainerToken), TestContext.Current.CancellationToken);

        ended.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RenewalsAsync()).ShouldHaveSingleItem().Status.ShouldBe(RenewalRequestStatus.LinkEnded);
    }

    [Fact]
    public async Task AnotherTrainerTakesTheMemberOver_ClosesTheWaitingRequest()
    {
        var gym = await AskedGymAsync(days: 7);
        await AddTrainerAsync("مهدی رحیمی", OtherTrainerPhone);
        var otherToken = await SignInAsync(gym.Client, OtherTrainerPhone);
        var requestId = await RequestIdAsync(gym, await TrainerIdAsync(gym, "مهدی رحیمی"));

        using var accepted = await SendJsonAsync(gym, $"/api/site/coaching/requests/{requestId}/accept", new { days = 30 }, otherToken);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RenewalsAsync()).ShouldHaveSingleItem().Status.ShouldBe(RenewalRequestStatus.LinkEnded);
    }

    [Fact]
    public async Task OwnerStopsTheTrainer_ClosesTheWaitingRequest()
    {
        await AskedGymAsync(days: 7);
        await using (var scope = Fixture.CreateScope())
        {
            var trainerId = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Trainers
                .Select(trainer => trainer.Id).SingleAsync(TestContext.Current.CancellationToken);
            (await scope.ServiceProvider.GetRequiredService<StopTrainerHandler>().Handle(trainerId, TestContext.Current.CancellationToken))
                .IsSuccess.ShouldBeTrue();
        }

        (await RenewalsAsync()).ShouldHaveSingleItem().Status.ShouldBe(RenewalRequestStatus.LinkEnded);
    }

    // ---- A member the desk deactivates ----

    [Fact]
    public async Task DeactivatedMember_LeftOutOfRequestsAndCannotBeAnsweredThere()
    {
        var gym = await AskedGymAsync(days: 7);
        var renewalId = await RenewalIdAsync();
        await DeactivateAsync(gym.MemberId);

        var students = await GetJsonAsync(gym.Client, StudentsPath, gym.TrainerToken);
        using var accepted = await AcceptAsync(gym, renewalId, new { days = 30 });
        using var declined = await DeclineAsync(gym, renewalId);

        students.GetProperty("renewalRequests").GetArrayLength().ShouldBe(0);
        accepted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        declined.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await RenewalsAsync()).ShouldHaveSingleItem().IsPending.ShouldBeTrue();
    }

    [Fact]
    public async Task DeactivatedMember_RenewOnTheSchedulePageStillAnswersIt()
    {
        var gym = await AskedGymAsync(days: 7);
        await DeactivateAsync(gym.MemberId);

        using var renewed = await SendJsonAsync(gym, $"{StudentsPath}/{gym.MemberId}/period", new { days = 30 });

        renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RenewalsAsync()).ShouldHaveSingleItem().Status.ShouldBe(RenewalRequestStatus.Renewed);
    }

    // ---- The database holds it too ----

    [Fact]
    public async Task Insert_SecondWaitingForTheLink_RejectedByTheUniqueIndex()
    {
        await AskedGymAsync(days: 7);
        var linkId = (await RenewalsAsync()).Single().LinkId;

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertAsync(linkId, "Pending", answeredOn: null));

        exception.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        exception.ConstraintName.ShouldBe("ux_renewal_requests_one_pending_per_link");
    }

    [Fact]
    public async Task Insert_AnsweredWithoutADay_RejectedByTheCheckConstraint()
    {
        await LinkedGymAsync(days: 7);
        var linkId = (await PeriodsAsync()).Single().LinkId;

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertAsync(linkId, "Declined", answeredOn: null));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("ck_renewal_requests_answered_on_with_answer");
    }

    [Fact]
    public async Task Insert_AnsweredBeforeItWasAsked_RejectedByTheCheckConstraint()
    {
        await LinkedGymAsync(days: 7);
        var linkId = (await PeriodsAsync()).Single().LinkId;

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertAsync(linkId, "Renewed", answeredOn: Today().AddDays(-1)));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("ck_renewal_requests_answered_on_not_before_requested_on");
    }

    [Fact]
    public async Task Insert_UnknownStatus_RejectedByTheCheckConstraint()
    {
        await LinkedGymAsync(days: 7);
        var linkId = (await PeriodsAsync()).Single().LinkId;

        var exception = await Should.ThrowAsync<PostgresException>(() => InsertAsync(linkId, "Accepted", answeredOn: null));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe("ck_renewal_requests_status");
    }

    // ---- Helpers ----

    /// <summary>A trainer and a member, both signed in, not linked.</summary>
    private async Task<Gym> GymAsync()
    {
        var member = await AddMemberAsync();
        await AddTrainerAsync("رضا محمدی", TrainerPhone);
        var client = Fixture.CreateClient();

        return new Gym(client, await SignInAsync(client, TrainerPhone), await SignInAsync(client, MemberPhone), member.Id);
    }

    /// <summary>The member is the trainer's student from today, for <paramref name="days"/> days.</summary>
    private async Task<Gym> LinkedGymAsync(int days)
    {
        var gym = await GymAsync();
        var requestId = await RequestIdAsync(gym, await TrainerIdAsync(gym, "رضا محمدی"));
        using var accepted = await SendJsonAsync(gym, $"/api/site/coaching/requests/{requestId}/accept", new { days });
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);

        return gym;
    }

    /// <summary>A student who has asked for a renewal, waiting.</summary>
    private async Task<Gym> AskedGymAsync(int days)
    {
        var gym = await LinkedGymAsync(days);
        using var asked = await AskAsync(gym);
        asked.StatusCode.ShouldBe(HttpStatusCode.OK);

        return gym;
    }

    private static async Task<Guid> RequestIdAsync(Gym gym, Guid trainerId)
    {
        using var asked = await SendJsonAsync(gym, "/api/site/coaching/my-trainer/request", new { trainerId }, gym.MemberToken);
        asked.StatusCode.ShouldBe(HttpStatusCode.OK);

        return (await asked.ReadJsonAsync()).GetProperty("request").GetProperty("requestId").GetGuid();
    }

    private static async Task<Guid> TrainerIdAsync(Gym gym, string fullName)
    {
        var trainers = await GetJsonAsync(gym.Client, "/api/site/coaching/trainers", gym.MemberToken);

        return trainers.EnumerateArray().Single(trainer => trainer.GetProperty("fullName").GetString() == fullName)
            .GetProperty("trainerId").GetGuid();
    }

    private async Task<Guid> RenewalIdAsync() => (await RenewalsAsync()).OrderBy(request => request.CreatedAt).Last().Id;

    private static Task<HttpResponseMessage> AskAsync(Gym gym) =>
        gym.Client.SendAsync(Send(HttpMethod.Post, RenewalPath, gym.MemberToken), TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> CancelAsync(Gym gym) =>
        gym.Client.SendAsync(Send(HttpMethod.Post, $"{RenewalPath}/cancel", gym.MemberToken), TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> AcceptAsync(Gym gym, Guid renewalId, object body, string? token = null) =>
        SendJsonAsync(gym, $"/api/site/coaching/renewal-requests/{renewalId}/accept", body, token);

    private static Task<HttpResponseMessage> DeclineAsync(Gym gym, Guid renewalId, string? token = null) =>
        gym.Client.SendAsync(
            Send(HttpMethod.Post, $"/api/site/coaching/renewal-requests/{renewalId}/decline", token ?? gym.TrainerToken),
            TestContext.Current.CancellationToken);

    private static Task<HttpResponseMessage> SetDaysLeftAsync(Gym gym, int daysLeft) =>
        SendJsonAsync(gym, $"{StudentsPath}/{gym.MemberId}/period/days-left", new { daysLeft });

    /// <summary>Moves the link's only period <paramref name="daysBack"/> days back, as if accepted then.</summary>
    private async Task MovePeriodBackAsync(int daysBack)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE coaching_periods SET starts_on = starts_on - {daysBack}, ends_on = ends_on - {daysBack}",
            TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlAsync(
            $"UPDATE trainer_links SET started_on = started_on - {daysBack}",
            TestContext.Current.CancellationToken);
    }

    private async Task SetSoonDaysAsync(int days)
    {
        await using var scope = Fixture.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"UPDATE sms_settings SET coaching_period_ending_days = {days}", TestContext.Current.CancellationToken);
    }

    private async Task InsertAsync(Guid linkId, string status, DateOnly? answeredOn)
    {
        await using var scope = Fixture.CreateScope();
        var today = Today();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"""
            INSERT INTO renewal_requests (id, link_id, requested_on, period_ends_on, status, answered_on, created_at)
            VALUES ({Guid.CreateVersion7()}, {linkId}, {today}, {today.AddDays(6)}, {status}, {answeredOn}, now())
            """,
            TestContext.Current.CancellationToken);
    }

    private async Task DeactivateAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Members.SingleAsync(row => row.Id == memberId, TestContext.Current.CancellationToken);
        member.Deactivate();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static HttpRequestMessage Send(HttpMethod method, string path, string token) => new HttpRequestMessage(method, path).WithBearer(token);

    /// <summary>Posts <paramref name="body"/> as the trainer, or as <paramref name="token"/>'s account.</summary>
    private static Task<HttpResponseMessage> SendJsonAsync(Gym gym, string path, object body, string? token = null)
    {
        var request = Send(HttpMethod.Post, path, token ?? gym.TrainerToken);
        request.Content = JsonContent.Create(body);

        return gym.Client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path, string token)
    {
        using var response = await client.SendAsync(Send(HttpMethod.Get, path, token), TestContext.Current.CancellationToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        return await response.ReadJsonAsync();
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

    private async Task<Member> AddMemberAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = TestMembers.Seed("علی کریمی", MemberPhone);
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task AddTrainerAsync(string fullName, string phone)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Trainers.Add(Trainer.Create(fullName, phone, new DateOnly(2026, 10, 1)).Value);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<CoachingPeriod>> PeriodsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().CoachingPeriods.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<RenewalRequest>> RenewalsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().RenewalRequests.AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private sealed record Gym(HttpClient Client, string TrainerToken, string MemberToken, Guid MemberId);
}
