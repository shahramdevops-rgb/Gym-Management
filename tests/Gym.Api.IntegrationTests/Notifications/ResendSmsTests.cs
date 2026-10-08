using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications;
using Gym.Application.Notifications.ResendSms;
using Gym.Domain.Common;
using Gym.Domain.Notifications;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The Owner's resend (BUSINESS_RULES.md §10 <i>Sending</i>, task 10.5): exactly the same message, one
/// request, never outside 08:00–22:00. Built the way the endpoint builds <see cref="ResendSmsHandler"/>,
/// on a clock pinned to Tehran's 10:00 on <see cref="Today"/>, with a sender that answers what each
/// test scripts. The endpoint's own refusals are in <see cref="SmsMessagesEndpointTests"/>.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class ResendSmsTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Name = "سارا محمدی";
    private const string Template = "gymBirthday";

    /// <summary>1405/07/16.</summary>
    private static readonly DateOnly Today = new(2026, 10, 8);

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    private static readonly SmsTokens Tokens = new("۱۴۰۵", Token10: Name);

    private static int _phoneSuffix;

    // ---- What is sent ----

    [Theory]
    [InlineData(NotificationStatus.Failed)]
    [InlineData(NotificationStatus.Unknown)]
    public async Task Handle_FailedOrUnknown_SendsExactlyTheSameMessageAndIsSent(NotificationStatus status)
    {
        var (id, phone) = await AddAsync(status);
        var sender = new ScriptedSmsSender();

        var result = await ResendAsync(id, sender);

        var request = sender.Requests.ShouldHaveSingleItem();
        request.ShouldBe(new SmsTemplateMessage(phone, Template, Tokens));
        var message = result.Value;
        message.Status.ShouldBe(NotificationStatus.Sent);
        message.MemberName.ShouldBe(Name);
        message.CostToman.ShouldBe(ScriptedSmsSender.Cost / 10m);

        var stored = await StoredAsync(id);
        stored.Status.ShouldBe(NotificationStatus.Sent);
        stored.Attempts.ShouldBe(2);
        stored.ErrorCode.ShouldBeNull();
        stored.SentAt.ShouldBe(At(Today, 10, 0));
    }

    [Fact]
    public async Task Handle_MembersNumberChangedSince_StillGoesToTheNumberItWasWrittenFor()
    {
        var (id, phone) = await AddAsync(NotificationStatus.Failed);
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var member = await db.Members.SingleAsync(TestContext.Current.CancellationToken);
            member.Update(Name, "+989199999999", member.Notes, member.BirthDate, Today).IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sender = new ScriptedSmsSender();

        await ResendAsync(id, sender);

        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(phone);
    }

    [Fact]
    public async Task Handle_AllSmsAndItsKindOff_StillSends()
    {
        // The settings start with everything off (§10); a resend is the Owner's own choice.
        var (id, _) = await AddAsync(NotificationStatus.Failed);
        var sender = new ScriptedSmsSender();

        var result = await ResendAsync(id, sender);

        result.IsSuccess.ShouldBeTrue();
        sender.Requests.Count.ShouldBe(1);
    }

    // ---- How it went ----

    [Fact]
    public async Task Handle_FailureThatMayPass_IsFailedAtOnceWithItsCode()
    {
        var (id, _) = await AddAsync(NotificationStatus.Failed);
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.RetryableFailure, ErrorCode: 409));

        var result = await ResendAsync(id, sender);

        sender.Requests.Count.ShouldBe(1);
        result.Value.Status.ShouldBe(NotificationStatus.Failed);
        result.Value.ErrorCode.ShouldBe(409);
    }

    [Fact]
    public async Task Handle_FailureThatWillNotPass_IsFailedWithItsCode()
    {
        var (id, _) = await AddAsync(NotificationStatus.Unknown);
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.PermanentFailure, ErrorCode: 424));

        var result = await ResendAsync(id, sender);

        result.Value.Status.ShouldBe(NotificationStatus.Failed);
        result.Value.ErrorCode.ShouldBe(424);
    }

    [Fact]
    public async Task Handle_CreditUsedUp_IsFailedWithItsCode()
    {
        var (id, _) = await AddAsync(NotificationStatus.Failed);
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.CreditExhausted, ErrorCode: SmsProviderCodes.CreditUsedUp));

        var result = await ResendAsync(id, sender);

        result.Value.Status.ShouldBe(NotificationStatus.Failed);
        result.Value.ErrorCode.ShouldBe(SmsProviderCodes.CreditUsedUp);
    }

    [Fact]
    public async Task Handle_NoAnswer_IsUnknown()
    {
        var (id, _) = await AddAsync(NotificationStatus.Failed);
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.Unknown));

        var result = await ResendAsync(id, sender);

        result.Value.Status.ShouldBe(NotificationStatus.Unknown);
        result.Value.ErrorCode.ShouldBeNull();
        (await StoredAsync(id)).Attempts.ShouldBe(2);
    }

    // ---- Refused ----

    [Theory]
    [InlineData(NotificationStatus.Sent)]
    [InlineData(NotificationStatus.Pending)]
    public async Task Handle_SentOrPending_IsRefusedAndSendsNothing(NotificationStatus status)
    {
        var (id, _) = await AddAsync(status);
        var sender = new ScriptedSmsSender();

        var result = await ResendAsync(id, sender);

        result.Error.ShouldBe(NotificationErrors.NotResendable);
        sender.Requests.ShouldBeEmpty();
        (await StoredAsync(id)).Status.ShouldBe(status);
    }

    [Fact]
    public async Task Handle_UnknownId_IsNotFound()
    {
        var result = await ResendAsync(Guid.CreateVersion7(), new ScriptedSmsSender());

        result.Error.ShouldBe(NotificationErrors.NotFound);
    }

    [Theory]
    [InlineData(7, 59, 59)]
    [InlineData(22, 0, 1)]
    [InlineData(0, 30, 0)]
    public async Task Handle_OutsideTheSendingHours_IsRefusedAndChangesNothing(int hour, int minute, int second)
    {
        var (id, _) = await AddAsync(NotificationStatus.Failed);
        var sender = new ScriptedSmsSender();

        var result = await ResendAsync(id, sender, At(Today, hour, minute, second));

        result.Error.ShouldBe(NotificationErrors.OutsideSendingHours);
        sender.Requests.ShouldBeEmpty();
        var stored = await StoredAsync(id);
        stored.Status.ShouldBe(NotificationStatus.Failed);
        stored.ErrorCode.ShouldBe(424);
    }

    [Theory]
    [InlineData(8, 0)]
    [InlineData(22, 0)]
    public async Task Handle_AtTheEdgesOfTheSendingHours_Sends(int hour, int minute)
    {
        var (id, _) = await AddAsync(NotificationStatus.Failed);

        var result = await ResendAsync(id, new ScriptedSmsSender(), At(Today, hour, minute));

        result.Value.Status.ShouldBe(NotificationStatus.Sent);
    }

    // ---- At the same moment ----

    [Fact]
    public async Task Handle_SecondClickWhileTheFirstIsSending_IsRefusedAndOnlyOneRequestLeaves()
    {
        var (id, _) = await AddAsync(NotificationStatus.Failed);
        Result<SmsMessageResponse>? second = null;
        var secondSender = new ScriptedSmsSender();
        var first = new DuringSendSmsSender(async () => second = await ResendAsync(id, secondSender));

        var result = await ResendAsync(id, first);

        result.Value.Status.ShouldBe(NotificationStatus.Sent);
        second.ShouldNotBeNull().Error.ShouldBe(NotificationErrors.NotResendable);
        secondSender.Requests.ShouldBeEmpty();
        (await StoredAsync(id)).Attempts.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_TwoLoadedBeforeEitherSaves_OnlyOneIsPrepared()
    {
        // The case the status check cannot see: both read Failed. xmin lets one save through.
        var (id, _) = await AddAsync(NotificationStatus.Failed);
        await using var firstScope = Fixture.CreateScope();
        await using var secondScope = Fixture.CreateScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstRow = await firstDb.Notifications.SingleAsync(n => n.Id == id, TestContext.Current.CancellationToken);
        var secondRow = await secondDb.Notifications.SingleAsync(n => n.Id == id, TestContext.Current.CancellationToken);

        firstRow.PrepareResend().IsSuccess.ShouldBeTrue();
        secondRow.PrepareResend().IsSuccess.ShouldBeTrue();
        await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_RunMarksItUnknownWhileItIsSending_IsAConflictAndStaysUnknown()
    {
        // A run of the kind starting this very moment finds the row Pending and marks it Unknown.
        var (id, _) = await AddAsync(NotificationStatus.Failed);
        var sender = new DuringSendSmsSender(async () =>
        {
            await using var scope = Fixture.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var row = await db.Notifications.SingleAsync(n => n.Id == id, TestContext.Current.CancellationToken);
            row.MarkInterrupted().IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

        var result = await ResendAsync(id, sender);

        result.Error.ShouldBe(NotificationErrors.ChangedConcurrently);
        (await StoredAsync(id)).Status.ShouldBe(NotificationStatus.Unknown);
    }

    // ---- Helpers ----

    private async Task<Result<SmsMessageResponse>> ResendAsync(Guid id, ISmsSender sender, DateTimeOffset? at = null)
    {
        var time = new FakeTimeProvider(at ?? At(Today, 10, 0));
        var calendar = new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        return await new ResendSmsHandler(db, calendar, time, sender).Handle(id, TestContext.Current.CancellationToken);
    }

    /// <summary>A birthday SMS for a new member, left in <paramref name="status"/> by one request yesterday.</summary>
    private async Task<(Guid Id, string Phone)> AddAsync(NotificationStatus status)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = TestMembers.Seed(Name, $"+98914{suffix:D7}");
        var notification = Notification.ForBirthday(member.Id, 1405, member.PhoneNumber, Template, Tokens);
        var yesterday = At(Today.AddDays(-1), 10, 0);
        switch (status)
        {
            case NotificationStatus.Sent:
                notification.MarkSent(suffix, 1_350m, yesterday);
                break;
            case NotificationStatus.Failed:
                notification.MarkFailed(424, yesterday);
                break;
            case NotificationStatus.Unknown:
                notification.MarkUnknown(yesterday);
                break;
        }

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (notification.Id, member.PhoneNumber);
    }

    private async Task<Notification> StoredAsync(Guid id)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Notifications.AsNoTracking().SingleAsync(n => n.Id == id, TestContext.Current.CancellationToken);
    }

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute, int second = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute, second), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }

    /// <summary>Runs <paramref name="during"/> while the request is "in flight", then answers "sent".</summary>
    private sealed class DuringSendSmsSender(Func<Task> during) : ISmsSender
    {
        public async Task<SmsSendResult> SendTemplateAsync(SmsTemplateMessage message, CancellationToken cancellationToken)
        {
            await during();

            return SmsSendResult.Sent(1, ScriptedSmsSender.Cost);
        }
    }
}
