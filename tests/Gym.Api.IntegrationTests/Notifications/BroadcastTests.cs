using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications;
using Gym.Application.Notifications.Broadcasts;
using Gym.Application.Notifications.Broadcasts.ListBroadcasts;
using Gym.Application.Notifications.Broadcasts.PreviewBroadcast;
using Gym.Application.Notifications.Broadcasts.RecoverBroadcasts;
using Gym.Application.Notifications.Broadcasts.ResumeBroadcast;
using Gym.Application.Notifications.Broadcasts.RunBroadcast;
using Gym.Application.Notifications.Broadcasts.SendBroadcast;
using Gym.Application.Notifications.ListSmsMessages;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Persistence;
using Gym.Infrastructure.Persistence.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using Npgsql;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The broadcast (BUSINESS_RULES.md §10 <i>The broadcast</i>, task 10.13). Each handler is built the
/// way the endpoint or the job builds it, on a clock pinned to Tehran's hour each test needs, with a
/// sender that answers what each test scripts. The endpoints' own answers are in
/// <see cref="BroadcastEndpointTests"/>.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class BroadcastTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Text = "باشگاه فردا تا ساعت ۱۶ تعطیل است.";

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    /// <summary>1405/07/27.</summary>
    private static readonly DateOnly Today = new(2026, 10, 19);

    private static readonly SmsRetrySchedule NoWait = new(3, [TimeSpan.Zero, TimeSpan.Zero]);

    private static int _phoneSuffix;

    // ---- Sending ----

    [Fact]
    public async Task Send_ActiveMembers_SavesItSendingAndAsksForTheJob()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        await AddMemberAsync("غیرفعال", active: false);
        var scheduler = Scheduler();

        var result = await SendAsync(Text, BroadcastAudience.ActiveMembers);

        var broadcast = result.Value;
        broadcast.Status.ShouldBe(BroadcastStatus.Sending);
        broadcast.Text.ShouldBe(Text);
        scheduler.Asked.ShouldHaveSingleItem().ShouldBe(broadcast.Id);
        (await BroadcastsAsync()).ShouldHaveSingleItem().Status.ShouldBe(BroadcastStatus.Sending);

        // The messages are the job's to write, one at a time.
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Send_AllSmsOrTheBroadcastOff_IsRefused(bool allSms, bool broadcast)
    {
        await TurnOnAsync(allSms, broadcast);
        await AddMemberAsync("علی رضایی");

        var result = await SendAsync(Text, BroadcastAudience.ActiveMembers);

        result.Error.ShouldBe(SmsBroadcastErrors.Off);
        (await BroadcastsAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(7, 59)]
    [InlineData(22, 1)]
    public async Task Send_OutsideTheSendingHours_IsRefused(int hour, int minute)
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");

        var result = await SendAsync(Text, BroadcastAudience.ActiveMembers, At(Today, hour, minute));

        result.Error.ShouldBe(NotificationErrors.OutsideSendingHours);
    }

    [Fact]
    public async Task Send_NobodyInTheGroup_IsRefused()
    {
        await TurnOnAsync();
        await AddMemberAsync("بدون پلن");

        var result = await SendAsync(Text, BroadcastAudience.MembersWithUsablePlan);

        result.Error.ShouldBe(SmsBroadcastErrors.NoRecipients);
    }

    [Fact]
    public async Task Send_TooLongForTheLongestFirstNameInTheGroup_IsRefused()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        var withPlan = await AddMemberAsync("عبدالرحمن کریمی");
        var text = new string('ب', 180);

        // «علی عزیز، » + 180 + «\nلغو11» is 196: three parts. «عبدالرحمن» makes it 202: four.
        (await SendAsync(text, BroadcastAudience.ActiveMembers)).Error.ShouldBe(SmsBroadcastErrors.TooLong);

        await DeactivateAsync(withPlan.Id);
        (await SendAsync(text, BroadcastAudience.ActiveMembers)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Send_AnotherStillSending_IsRefused()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        (await SendAsync(Text, BroadcastAudience.ActiveMembers)).IsSuccess.ShouldBeTrue();

        var second = await SendAsync("متن دیگر", BroadcastAudience.ActiveMembers);

        second.Error.ShouldBe(SmsBroadcastErrors.InProgress);
        (await BroadcastsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Database_SecondBroadcastSending_IsRefusedByTheIndex()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.SmsBroadcasts.Add(SmsBroadcast.Create("یک", BroadcastAudience.ActiveMembers).Value);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        db.SmsBroadcasts.Add(SmsBroadcast.Create("دو", BroadcastAudience.ActiveMembers).Value);
        var exception = await Should.ThrowAsync<UniqueConstraintException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        exception.ConstraintName.ShouldBe(SmsBroadcastConfiguration.OneSending);
    }

    [Fact]
    public async Task Send_Saved_IsInTheAuditLog()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");

        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;

        await using var scope = Fixture.CreateScope();
        var insert = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.AsNoTracking()
            .SingleAsync(
                log => log.EntityType == nameof(SmsBroadcast) && log.Action == Domain.Audit.AuditAction.Insert,
                TestContext.Current.CancellationToken);
        insert.EntityId.ShouldBe(id.ToString());
        using var values = System.Text.Json.JsonDocument.Parse(insert.NewValues.ShouldNotBeNull());
        values.RootElement.GetProperty(nameof(SmsBroadcast.Text)).GetString().ShouldBe(Text);
    }

    [Fact]
    public async Task Send_JobRunnerDown_StopsTheBroadcastSoAnotherCanGo()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        Scheduler().Fail = true;

        var result = await SendAsync(Text, BroadcastAudience.ActiveMembers);

        result.Value.Status.ShouldBe(BroadcastStatus.Stopped);
        result.Value.StopReason.ShouldBe(BroadcastStopReason.Error);
        Scheduler().Fail = false;
        (await SendAsync("دوباره", BroadcastAudience.ActiveMembers)).IsSuccess.ShouldBeTrue();
    }

    // ---- The job ----

    [Fact]
    public async Task Run_EveryoneInTheGroup_GetsTheTextWithTheirFirstNameOnce()
    {
        await TurnOnAsync();
        var ali = await AddMemberAsync("علی رضایی");
        var sayyed = await AddMemberAsync("سید محمد احمدی");
        await AddMemberAsync("غیرفعال", active: false);
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;
        var sender = new ScriptedSmsSender();

        var run = await RunAsync(id, sender);
        await RunAsync(id, sender);

        run.Status.ShouldBe(BroadcastStatus.Finished);
        run.Sent.ShouldBe(2);
        sender.Requests.Select(request => (request.Receptor, request.Text)).OrderBy(pair => pair.Receptor).ShouldBe(
            new[]
            {
                (ali.PhoneNumber, $"علی عزیز، {Text}\nلغو11"),
                (sayyed.PhoneNumber, $"محمد عزیز، {Text}\nلغو11"),
            }.OrderBy(pair => pair.Item1));

        var messages = await NotificationsAsync();
        messages.Count.ShouldBe(2);
        messages.ShouldAllBe(message => message.Kind == NotificationKind.Broadcast && message.BroadcastId == id
            && message.Status == NotificationStatus.Sent);
        var broadcast = (await BroadcastsAsync()).ShouldHaveSingleItem();
        broadcast.Status.ShouldBe(BroadcastStatus.Finished);
        broadcast.EndedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Run_PlanGroup_ReachesOnlyMembersWithAPlanUsableToday()
    {
        await TurnOnAsync();
        var usable = await AddMemberAsync("پلن فعال");
        await AddPlanAsync(usable.Id, Today.AddDays(-5));
        var frozen = await AddMemberAsync("فریز");
        await AddPlanAsync(frozen.Id, Today.AddDays(-5), frozenSince: Today.AddDays(-2));
        var ended = await AddMemberAsync("تمام‌شده");
        await AddPlanAsync(ended.Id, Today.AddDays(-45));
        var later = await AddMemberAsync("پلن آینده");
        await AddPlanAsync(later.Id, Today.AddDays(3));
        await AddMemberAsync("بدون پلن");
        var id = (await SendAsync(Text, BroadcastAudience.MembersWithUsablePlan)).Value.Id;
        var sender = new ScriptedSmsSender();

        await RunAsync(id, sender);

        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(usable.PhoneNumber);
    }

    [Fact]
    public async Task Run_CreditUsedUp_StopsAndContinuingReachesOnlyTheRest()
    {
        await TurnOnAsync();
        var members = new List<Member>();
        for (var i = 0; i < 4; i++)
        {
            members.Add(await AddMemberAsync($"عضو {i}"));
        }

        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;
        var first = new ScriptedSmsSender(
            SmsSendResult.Sent(1, ScriptedSmsSender.Cost),
            new SmsSendResult(SmsSendOutcome.CreditExhausted, ErrorCode: 418));

        var stopped = await RunAsync(id, first);

        stopped.Status.ShouldBe(BroadcastStatus.Stopped);
        first.Requests.Count.ShouldBe(2);
        (await NotificationsAsync()).Count.ShouldBe(2);
        (await BroadcastsAsync()).ShouldHaveSingleItem().StopReason.ShouldBe(BroadcastStopReason.CreditExhausted);

        var resumed = await ResumeAsync(id);
        resumed.Value.Status.ShouldBe(BroadcastStatus.Sending);
        Scheduler(clear: false).Asked.ShouldContain(id);

        var second = new ScriptedSmsSender();
        (await RunAsync(id, second)).Status.ShouldBe(BroadcastStatus.Finished);

        // The two not written yet; the one that failed for credit is the Owner's to resend.
        second.Requests.Select(request => request.Receptor)
            .ShouldBe(members.Skip(2).Select(member => member.PhoneNumber), ignoreOrder: true);
        (await NotificationsAsync()).Select(message => message.MemberId).Distinct().Count().ShouldBe(4);
    }

    [Fact]
    public async Task Run_RetryableFailure_IsTriedAgain()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.RetryableFailure, ErrorCode: 409));

        var run = await RunAsync(id, sender);

        run.Sent.ShouldBe(1);
        sender.Requests.Count.ShouldBe(2);
        (await NotificationsAsync()).ShouldHaveSingleItem().Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Run_LeftPendingByAStoppedJob_IsUnknownAndTheRestAreSent()
    {
        await TurnOnAsync();
        var first = await AddMemberAsync("علی رضایی");
        var second = await AddMemberAsync("مریم کاظمی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Notifications.Add(Notification.ForBroadcast(id, first.Id, first.PhoneNumber, SmsText.ForBroadcast(first.FullName, Text)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sender = new ScriptedSmsSender();
        var run = await RunAsync(id, sender);

        run.Interrupted.ShouldBe(1);
        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(second.PhoneNumber);
        (await NotificationsAsync()).Single(message => message.MemberId == first.Id).Status.ShouldBe(NotificationStatus.Unknown);
    }

    [Fact]
    public async Task Run_TurnedOffBeforeTheJobRan_Stops()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;
        await TurnOnAsync(broadcast: false);
        var sender = new ScriptedSmsSender();

        var run = await RunAsync(id, sender);

        run.Status.ShouldBe(BroadcastStatus.Stopped);
        sender.Requests.ShouldBeEmpty();
        (await BroadcastsAsync()).ShouldHaveSingleItem().StopReason.ShouldBe(BroadcastStopReason.TurnedOff);
    }

    [Fact]
    public async Task Run_MadeAfterTheHours_StopsAndCannotBeContinuedUntilMorning()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;

        var run = await RunAsync(id, new ScriptedSmsSender(), At(Today, 22, 30));

        run.Status.ShouldBe(BroadcastStatus.Stopped);
        (await BroadcastsAsync()).ShouldHaveSingleItem().StopReason.ShouldBe(BroadcastStopReason.OutsideSendingHours);
        (await ResumeAsync(id, At(Today, 23, 0))).Error.ShouldBe(NotificationErrors.OutsideSendingHours);
        (await ResumeAsync(id, At(Today.AddDays(1), 9, 0))).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Run_HoursEndWhileSending_StopsWithOutsideSendingHoursAndWritesNoMore()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        await AddMemberAsync("رضا کریمی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;
        AfterFirstSmsSender? sender = null;

        // The first message goes at 21:59; by the second it is 22:01.
        var run = await RunAsync(
            id,
            time => sender = new AfterFirstSmsSender(() =>
            {
                time.Advance(TimeSpan.FromMinutes(2));
                return Task.CompletedTask;
            }),
            At(Today, 21, 59));

        run.Status.ShouldBe(BroadcastStatus.Stopped);
        run.Sent.ShouldBe(1);
        sender.ShouldNotBeNull().Requests.Count.ShouldBe(1);
        (await NotificationsAsync()).ShouldHaveSingleItem().Status.ShouldBe(NotificationStatus.Sent);
        (await BroadcastsAsync()).ShouldHaveSingleItem().StopReason.ShouldBe(BroadcastStopReason.OutsideSendingHours);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Run_TurnedOffWhileSending_StopsWithTurnedOffAndWritesNoMore(bool allSms, bool broadcast)
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        await AddMemberAsync("رضا کریمی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;
        var sender = new AfterFirstSmsSender(() => TurnOnAsync(allSms, broadcast));

        var run = await RunAsync(id, sender);

        run.Status.ShouldBe(BroadcastStatus.Stopped);
        sender.Requests.Count.ShouldBe(1);
        (await NotificationsAsync()).ShouldHaveSingleItem().Status.ShouldBe(NotificationStatus.Sent);
        (await BroadcastsAsync()).ShouldHaveSingleItem().StopReason.ShouldBe(BroadcastStopReason.TurnedOff);
    }

    [Fact]
    public async Task Run_LongerFirstNameJoinedBeforeTheJob_StopsWithTooLongAndSendsNothing()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        // «علی عزیز، » + 180 + «\nلغو11» is 196: three parts. «عبدالرحمن» makes it 202: four.
        var id = (await SendAsync(new string('ب', 180), BroadcastAudience.ActiveMembers)).Value.Id;
        await AddMemberAsync("عبدالرحمن کریمی");
        var sender = new ScriptedSmsSender();

        var run = await RunAsync(id, sender);

        run.Status.ShouldBe(BroadcastStatus.Stopped);
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldBeEmpty();
        (await BroadcastsAsync()).ShouldHaveSingleItem().StopReason.ShouldBe(BroadcastStopReason.TooLong);
    }

    [Fact]
    public async Task Run_UnexpectedFailure_StopsTheBroadcastWithError()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;

        await Should.ThrowAsync<InvalidOperationException>(() => RunAsync(id, new ThrowingSmsSender()));

        (await BroadcastsAsync()).ShouldHaveSingleItem().StopReason.ShouldBe(BroadcastStopReason.Error);
    }

    [Fact]
    public async Task Database_SecondMessageToAMemberInOneBroadcast_IsRefusedByTheIndex()
    {
        await TurnOnAsync();
        var member = await AddMemberAsync("علی رضایی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.Add(Notification.ForBroadcast(id, member.Id, member.PhoneNumber, "یک"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Notifications.Add(Notification.ForBroadcast(id, member.Id, member.PhoneNumber, "دو"));

        var exception = await Should.ThrowAsync<UniqueConstraintException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        exception.ConstraintName.ShouldBe(NotificationConstraints.OnePerBroadcastAndMember);
    }

    [Fact]
    public async Task Database_BroadcastMessageWithoutItsBroadcast_IsRefusedByTheCheck()
    {
        var member = await AddMemberAsync("علی رضایی");

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var exception = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO notifications (id, kind, recipient, member_id, text, status, attempts, created_at)
            VALUES ({Guid.CreateVersion7()}, 'Broadcast', {member.PhoneNumber}, {member.Id}, 'متن', 'Pending', 0, now())
            """,
            TestContext.Current.CancellationToken));

        exception.ConstraintName.ShouldBe(NotificationConstraints.EventMatchesKind);
    }

    // ---- Continuing ----

    [Fact]
    public async Task Resume_NotStopped_IsRefused()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;

        (await ResumeAsync(id)).Error.ShouldBe(SmsBroadcastErrors.NotStopped);
        (await ResumeAsync(Guid.NewGuid())).Error.ShouldBe(SmsBroadcastErrors.NotFound);
    }

    [Fact]
    public async Task Resume_NobodyLeft_FinishesWithoutAJob()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;
        await RunAsync(id, new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.CreditExhausted, ErrorCode: 418)));
        var scheduler = Scheduler();

        var resumed = await ResumeAsync(id);

        resumed.Value.Status.ShouldBe(BroadcastStatus.Finished);
        resumed.Value.Failed.ShouldBe(1);
        scheduler.Asked.ShouldBeEmpty();
    }

    [Fact]
    public async Task Resume_AnotherSending_IsRefused()
    {
        var id = await StoppedByCreditAsync(Text, "علی رضایی", "رضا کریمی");
        (await SendAsync("متن دیگر", BroadcastAudience.ActiveMembers)).IsSuccess.ShouldBeTrue();

        (await ResumeAsync(id)).Error.ShouldBe(SmsBroadcastErrors.InProgress);
        (await BroadcastsAsync()).Single(broadcast => broadcast.Id == id).Status.ShouldBe(BroadcastStatus.Stopped);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Resume_AllSmsOrTheBroadcastOff_IsRefused(bool allSms, bool broadcast)
    {
        var id = await StoppedByCreditAsync(Text, "علی رضایی", "رضا کریمی");
        await TurnOnAsync(allSms, broadcast);

        (await ResumeAsync(id)).Error.ShouldBe(SmsBroadcastErrors.Off);
    }

    [Fact]
    public async Task Resume_LongerFirstNameJoinedTheGroup_IsRefusedAsTooLong()
    {
        // «علی عزیز، » + 180 + «\nلغو11» is 196: three parts. «عبدالرحمن» makes it 202: four.
        var id = await StoppedByCreditAsync(new string('ب', 180), "علی رضایی", "رضا کریمی");
        await AddMemberAsync("عبدالرحمن کریمی");

        (await ResumeAsync(id)).Error.ShouldBe(SmsBroadcastErrors.TooLong);
    }

    // ---- Recovering one left sending ----

    [Fact]
    public async Task RequeueSending_StillSendingAtStart_IsHandedToTheJobAgain()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        var finished = (await SendAsync("متن اول", BroadcastAudience.ActiveMembers)).Value.Id;
        await RunAsync(finished, new ScriptedSmsSender());
        var sending = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;
        var scheduler = Scheduler();

        var count = 0;
        await RecoverAsync(async handler => count = await handler.RequeueSendingAsync(TestContext.Current.CancellationToken));

        count.ShouldBe(1);
        scheduler.Asked.ShouldBe([sending]);
    }

    [Fact]
    public async Task StopAfterJobFailed_StillSending_StopsWithErrorSoAnotherCanGo()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;

        await RecoverAsync(handler => handler.StopAfterJobFailedAsync(id, TestContext.Current.CancellationToken));

        var broadcast = (await BroadcastsAsync()).ShouldHaveSingleItem();
        broadcast.Status.ShouldBe(BroadcastStatus.Stopped);
        broadcast.StopReason.ShouldBe(BroadcastStopReason.Error);
        (await SendAsync("متن دیگر", BroadcastAudience.ActiveMembers)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task StopAfterJobFailed_AlreadyFinished_ChangesNothing()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;
        await RunAsync(id, new ScriptedSmsSender());

        await RecoverAsync(handler => handler.StopAfterJobFailedAsync(id, TestContext.Current.CancellationToken));

        var broadcast = (await BroadcastsAsync()).ShouldHaveSingleItem();
        broadcast.Status.ShouldBe(BroadcastStatus.Finished);
        broadcast.StopReason.ShouldBeNull();
    }

    // ---- The confirmation's figures ----

    [Fact]
    public async Task Preview_Group_GivesRecipientsPartsTheEstimateAndTheLongestMessage()
    {
        await AddMemberAsync("علی رضایی");
        await AddMemberAsync("عبدالرحمن کریمی");
        await AddSentMessageAsync(text: new string('ب', 100), costRial: 2_000m);
        var text = new string('ب', 60);

        var preview = (await PreviewAsync(text, BroadcastAudience.ActiveMembers)).Value;

        // «علی عزیز، » + 60 + «\nلغو11» is 76: two parts; «عبدالرحمن» 82: two. The price is 2,000
        // Rial over two parts, 1,000 Rial a part: four parts are 4,000 Rial, 400 Toman.
        preview.Recipients.ShouldBe(2);
        preview.LongestMessageParts.ShouldBe(2);
        preview.TotalParts.ShouldBe(4);
        preview.EstimatedCostToman.ShouldBe(400m);
        preview.SampleText.ShouldBe($"عبدالرحمن عزیز، {text}\nلغو11");
    }

    [Fact]
    public async Task Preview_NothingChargedYet_HasNoEstimate()
    {
        await AddMemberAsync("علی رضایی");
        await AddSentMessageAsync(text: "پیامک", costRial: 0m);

        var preview = (await PreviewAsync(Text, BroadcastAudience.ActiveMembers)).Value;

        preview.TotalParts.ShouldBe(1);
        preview.EstimatedCostToman.ShouldBeNull();
    }

    [Fact]
    public async Task Preview_BlankOrTooLong_IsRefused()
    {
        await AddMemberAsync("علی رضایی");

        (await PreviewAsync("  ", BroadcastAudience.ActiveMembers)).Error.ShouldBe(SmsBroadcastErrors.TextRequired);
        (await PreviewAsync(new string('ب', 190), BroadcastAudience.ActiveMembers)).Error.ShouldBe(SmsBroadcastErrors.TooLong);
    }

    // ---- The lists ----

    [Fact]
    public async Task List_Broadcasts_LatestFirstWithTheirCounts()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        await AddMemberAsync("مریم کاظمی");
        var first = (await SendAsync("اول", BroadcastAudience.ActiveMembers)).Value.Id;
        await RunAsync(first, new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.PermanentFailure, ErrorCode: 411)));
        var second = (await SendAsync("دوم", BroadcastAudience.ActiveMembers)).Value.Id;

        await using var scope = Fixture.CreateScope();
        var page = await new ListBroadcastsHandler(scope.ServiceProvider.GetRequiredService<IAppDbContext>())
            .Handle(new ListBroadcastsQuery(), TestContext.Current.CancellationToken);

        page.TotalCount.ShouldBe(2);
        page.Items.Select(item => item.Id).ShouldBe([second, first]);
        var done = page.Items[1];
        (done.Sent, done.Failed, done.Unknown, done.Pending).ShouldBe((1, 1, 0, 0));
        done.CostToman.ShouldBe(ScriptedSmsSender.Cost / 10m);
    }

    [Fact]
    public async Task History_FilteredByBroadcast_ShowsOnlyItsMessages()
    {
        await TurnOnAsync();
        await AddMemberAsync("علی رضایی");
        var id = (await SendAsync(Text, BroadcastAudience.ActiveMembers)).Value.Id;
        await RunAsync(id, new ScriptedSmsSender());
        await AddSentMessageAsync(text: "پیامک دیگر", costRial: 1_000m);

        await using var scope = Fixture.CreateScope();
        var page = await scope.ServiceProvider.GetRequiredService<ListSmsMessagesHandler>()
            .Handle(new ListSmsMessagesQuery(BroadcastId: id), TestContext.Current.CancellationToken);

        var message = page.Items.ShouldHaveSingleItem();
        message.Kind.ShouldBe(NotificationKind.Broadcast);
        message.BroadcastId.ShouldBe(id);
        message.MemberName.ShouldBe("علی رضایی");
    }

    // ---- Helpers ----

    private async Task<Domain.Common.Result<BroadcastResponse>> SendAsync(
        string text, BroadcastAudience audience, DateTimeOffset? at = null)
    {
        var (time, calendar) = Clock(at ?? At(Today, 10, 0));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var handler = new SendBroadcastHandler(db, calendar, new BroadcastRecipients(db), Sending(db, calendar, time));

        return await handler.Handle(new SendBroadcastCommand(text, audience), TestContext.Current.CancellationToken);
    }

    private async Task<Domain.Common.Result<BroadcastResponse>> ResumeAsync(Guid id, DateTimeOffset? at = null)
    {
        var (time, calendar) = Clock(at ?? At(Today, 10, 30));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var handler = new ResumeBroadcastHandler(db, calendar, time, new BroadcastRecipients(db), Sending(db, calendar, time));

        return await handler.Handle(id, TestContext.Current.CancellationToken);
    }

    private async Task<Domain.Common.Result<BroadcastPreviewResponse>> PreviewAsync(string text, BroadcastAudience audience)
    {
        var (_, calendar) = Clock(At(Today, 10, 0));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        return await new PreviewBroadcastHandler(db, calendar, new BroadcastRecipients(db))
            .Handle(new PreviewBroadcastCommand(text, audience), TestContext.Current.CancellationToken);
    }

    /// <summary>The job as Hangfire makes it, at 10:15 in Tehran by default.</summary>
    private Task<BroadcastRunResult> RunAsync(Guid id, ISmsSender sender, DateTimeOffset? at = null) =>
        RunAsync(id, _ => sender, at ?? At(Today, 10, 15));

    /// <summary>The job, with a sender made on the job's own clock, so it can move it.</summary>
    private async Task<BroadcastRunResult> RunAsync(Guid id, Func<FakeTimeProvider, ISmsSender> makeSender, DateTimeOffset at)
    {
        var (time, calendar) = Clock(at);
        var sender = makeSender(time);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var handler = new RunBroadcastHandler(
            db,
            calendar,
            time,
            new SmsBatchSender(db, calendar, time, sender, NoWait),
            new BroadcastRecipients(db),
            NullLogger<RunBroadcastHandler>.Instance);

        return await handler.Handle(id, TestContext.Current.CancellationToken);
    }

    /// <summary>The recovery as the start and the Hangfire filter run it, at 10:30 in Tehran.</summary>
    private async Task RecoverAsync(Func<RecoverBroadcastsHandler, Task> act)
    {
        var (time, calendar) = Clock(At(Today, 10, 30));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        await act(new RecoverBroadcastsHandler(
            db, time, Sending(db, calendar, time), NullLogger<RecoverBroadcastsHandler>.Instance));
    }

    private BroadcastSending Sending(IAppDbContext db, GymCalendar calendar, TimeProvider time) =>
        new(db, calendar, time, Scheduler(clear: false), NullLogger<BroadcastSending>.Instance);

    /// <summary>The test host's scheduler (GymApiFactory), emptied of what earlier tests asked for.</summary>
    private RecordingBroadcastScheduler Scheduler(bool clear = true)
    {
        var scheduler = Fixture.Services.GetRequiredService<IBroadcastScheduler>().ShouldBeOfType<RecordingBroadcastScheduler>();
        if (clear)
        {
            scheduler.Clear();
        }

        return scheduler;
    }

    private static (FakeTimeProvider Time, GymCalendar Calendar) Clock(DateTimeOffset at)
    {
        var time = new FakeTimeProvider(at);

        return (time, new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" })));
    }

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }

    /// <summary>All SMS and the broadcast on, as the Owner would on the page; every other kind off.</summary>
    private async Task TurnOnAsync(bool allSms = true, bool broadcast = true)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SmsSettings.SingleAsync(TestContext.Current.CancellationToken);
        settings.Update(
            allSms,
            SmsKindSettings.Off,
            LowSessionsSettings.Off,
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            ownerPhone: null,
            dailyReportEnabled: false,
            dailyReportPhone: null,
            dailyReportSendTime: SmsSettings.DefaultDailyReportSendTime,
            subscriptionReceiptEnabled: false,
            welcomeEnabled: false,
            debtReminder: SmsKindSettings.Off,
            debtReminderMinAmount: null,
            absence: SmsKindSettings.Off,
            broadcastEnabled: broadcast).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Member> AddMemberAsync(string fullName, bool active = true)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(fullName, $"+98916{suffix:D7}");
        if (!active)
        {
            member.Deactivate();
        }

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    private async Task DeactivateAsync(Guid memberId)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var member = await db.Members.SingleAsync(row => row.Id == memberId, TestContext.Current.CancellationToken);
        member.Deactivate();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A 10-session, 30-day plan starting on <paramref name="startDate"/>.</summary>
    private async Task AddPlanAsync(Guid memberId, DateOnly startDate, DateOnly? frozenSince = null)
    {
        var plan = Subscription.CreateMembership(memberId, sessionCount: 10, sessionPrice: 90_000m, startDate).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Subscriptions.Add(plan);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        if (frozenSince is not null)
        {
            await db.Database.ExecuteSqlAsync(
                $"UPDATE subscriptions SET frozen_since = {frozenSince} WHERE id = {plan.Id}",
                TestContext.Current.CancellationToken);
        }
    }

    /// <summary>A daily report sent and charged, the latest message, whose cost the estimate reads.</summary>
    private async Task AddSentMessageAsync(string text, decimal costRial)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var day = await db.Notifications.CountAsync(TestContext.Current.CancellationToken);
        var message = Notification.ForDailyReport(Today.AddDays(-1 - day), "+989121112233", text);
        message.MarkSent(42, costRial, At(Today, 9, 0)).IsSuccess.ShouldBeTrue();
        db.Notifications.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<Notification>> NotificationsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Notifications
            .AsNoTracking()
            .Where(notification => notification.Kind == NotificationKind.Broadcast)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<SmsBroadcast>> BroadcastsAsync()
    {
        await using var scope = Fixture.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().SmsBroadcasts
            .AsNoTracking()
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// A broadcast to members with these names, whose first message met an empty account: stopped,
    /// with the rest still to go.
    /// </summary>
    private async Task<Guid> StoppedByCreditAsync(string text, params string[] fullNames)
    {
        await TurnOnAsync();
        foreach (var fullName in fullNames)
        {
            await AddMemberAsync(fullName);
        }

        var id = (await SendAsync(text, BroadcastAudience.ActiveMembers)).Value.Id;
        var run = await RunAsync(id, new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.CreditExhausted, ErrorCode: 418)));
        run.Status.ShouldBe(BroadcastStatus.Stopped);

        return id;
    }

    /// <summary>Sends every message; after the first request, does <paramref name="afterFirst"/> once.</summary>
    private sealed class AfterFirstSmsSender(Func<Task> afterFirst) : ISmsSender
    {
        private readonly ScriptedSmsSender _inner = new();
        private bool _done;

        public List<SmsMessage> Requests => _inner.Requests;

        public async Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken)
        {
            var result = await _inner.SendAsync(message, cancellationToken);
            if (!_done)
            {
                _done = true;
                await afterFirst();
            }

            return result;
        }
    }

    /// <summary>A provider whose client breaks in a way nobody planned for.</summary>
    private sealed class ThrowingSmsSender : ISmsSender
    {
        public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Something broke.");
    }
}
