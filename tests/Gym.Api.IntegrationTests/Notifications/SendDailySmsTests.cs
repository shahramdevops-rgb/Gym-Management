using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Common;
using Gym.Application.Common.Sms;
using Gym.Application.Notifications;
using Gym.Application.Notifications.SendDailySms;
using Gym.Domain.Common;
using Gym.Domain.Expenses;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.Payables;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Calendar;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The daily SMS runs (BUSINESS_RULES.md §10 <i>The four kinds</i> and <i>The daily runs</i>, task
/// 10.3). No endpoint calls the run, so these build <see cref="SendDailySmsHandler"/> the way the job
/// does, on a clock pinned to Tehran's 10:00 on <see cref="Today"/>, with a sender that answers what
/// each test scripts and never reaches a provider.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class SendDailySmsTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Name = "سارا محمدی";
    private const string OwnerPhone = "+989351112233";
    private const int Sessions = 10;
    private const int PlanDays = 30;

    /// <summary>1405/07/14.</summary>
    private static readonly DateOnly Today = new(2026, 10, 6);

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    /// <summary>Retries without waiting, for the tests about what is retried rather than when.</summary>
    private static readonly SmsRetrySchedule NoWait = new(3, [TimeSpan.Zero, TimeSpan.Zero]);

    private static readonly SmsRetrySchedule RealWaits = new(3, [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)]);

    /// <summary>Kavenegar busy (409): a failure that may pass.</summary>
    private static readonly SmsSendResult Retryable = new(SmsSendOutcome.RetryableFailure, ErrorCode: 409);

    private static int _phoneSuffix;

    // ---- When a run sends nothing ----

    [Fact]
    public async Task Handle_AllSmsOff_SendsNothing()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3, allSms: false);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        var sender = new ScriptedSmsSender();

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        result.Ran.ShouldBeFalse();
        sender.Requests.ShouldBeEmpty();
        (await NotificationsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_KindOff_SendsNothing()
    {
        await TurnOnAsync(NotificationKind.LowSessions, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        var sender = new ScriptedSmsSender();

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        result.Ran.ShouldBeFalse();
        sender.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(7, 59, 59)]
    [InlineData(22, 0, 1)]
    public async Task Handle_OutsideTheSendingHours_SendsNothing(int hour, int minute, int second)
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        var sender = new ScriptedSmsSender();

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender, at: At(Today, hour, minute, second));

        result.Ran.ShouldBeFalse();
        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_At2200_Sends()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        var sender = new ScriptedSmsSender();

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender, at: At(Today, 22, 0));

        result.Sent.ShouldBe(1);
    }

    // ---- Subscription running out ----

    [Fact]
    public async Task Handle_SubscriptionExpiring_SendsToThoseWithinTheDaysOnly()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        var inside = await AddMemberAsync();
        var plan = await AddPlanEndingInAsync(inside, daysLeft: 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 4);
        var sender = new ScriptedSmsSender();

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        result.ShouldBe(new SmsRunResult(Ran: true, Sent: 1));
        var request = sender.Requests.ShouldHaveSingleItem();
        request.Receptor.ShouldBe(inside.PhoneNumber);
        request.Text.ShouldBe(SmsText.ForRunningOut(Name, plan.EndDate, plan.RemainingSessions));

        var stored = (await NotificationsAsync()).ShouldHaveSingleItem();
        stored.Kind.ShouldBe(NotificationKind.SubscriptionExpiring);
        stored.MemberId.ShouldBe(inside.Id);
        stored.SubscriptionId.ShouldBe(plan.Id);
        stored.Status.ShouldBe(NotificationStatus.Sent);
        stored.Attempts.ShouldBe(1);
        stored.CostRial.ShouldBe(ScriptedSmsSender.Cost);
        stored.ProviderMessageId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Handle_SubscriptionExpiring_LastDayIsIncluded()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 1);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 0);

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, new ScriptedSmsSender());

        result.Sent.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_SubscriptionExpiring_RenewedMemberIsLeftOut()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        var member = await AddMemberAsync();
        var plan = await AddPlanEndingInAsync(member, daysLeft: 2);
        await AddSubscriptionAsync(Subscription.CreateMembership(member.Id, Sessions, 90_000m, plan.EndDate.AddDays(1)).Value);
        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_SubscriptionExpiring_RenewalCancelled_MemberCountsAsNotRenewed()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        var member = await AddMemberAsync();
        var plan = await AddPlanEndingInAsync(member, daysLeft: 2);
        var renewal = Subscription.CreateMembership(member.Id, Sessions, 90_000m, plan.EndDate.AddDays(1)).Value;
        renewal.Cancel("اشتباه ثبت شد", Today, At(Today, 9, 0)).IsSuccess.ShouldBeTrue();
        await AddSubscriptionAsync(renewal);
        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        sender.Requests.ShouldHaveSingleItem().Receptor.ShouldBe(member.PhoneNumber);
    }

    [Fact]
    public async Task Handle_SubscriptionExpiring_FrozenSingleVisitAndDeactivatedAreLeftOut()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 30);

        var frozen = Subscription.CreateMembership((await AddMemberAsync()).Id, Sessions, 90_000m, Today.AddDays(-20)).Value;
        frozen.Freeze(Today, maxFreezeDays: 30).IsSuccess.ShouldBeTrue();
        await AddSubscriptionAsync(frozen);

        await AddSubscriptionAsync(Subscription.CreateSingleVisit((await AddMemberAsync()).Id, 50_000m, Today).Value);

        await AddPlanEndingInAsync(await AddMemberAsync(active: false), daysLeft: 2);
        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_RunTwice_SendsNothingTwice()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 3);
        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.SubscriptionExpiring, sender);
        var second = await RunAsync(NotificationKind.SubscriptionExpiring, sender, at: At(Today.AddDays(1), 10, 0));

        second.Sent.ShouldBe(0);
        sender.Requests.Count.ShouldBe(1);
        (await NotificationsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Handle_NumberRaisedBetweenRuns_ReachesOnlyTheNewlyCovered()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        var near = await AddMemberAsync();
        await AddPlanEndingInAsync(near, daysLeft: 2);
        var far = await AddMemberAsync();
        await AddPlanEndingInAsync(far, daysLeft: 6);
        var sender = new ScriptedSmsSender();
        await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 7);
        await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        sender.Requests.Select(request => request.Receptor).ShouldBe([near.PhoneNumber, far.PhoneNumber]);
    }

    // ---- Few sessions left ----

    [Fact]
    public async Task Handle_LowSessions_SendsAtTheNumberNotAboveAndNotWhenUsedUp()
    {
        await TurnOnAsync(NotificationKind.LowSessions, 2);
        var atTheNumber = await AddMemberAsync();
        await AddPlanWithSessionsLeftAsync(atTheNumber, left: 2);
        await AddPlanWithSessionsLeftAsync(await AddMemberAsync(), left: 3);
        await AddPlanWithSessionsLeftAsync(await AddMemberAsync(), left: 0);
        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.LowSessions, sender);

        var request = sender.Requests.ShouldHaveSingleItem();
        request.Receptor.ShouldBe(atTheNumber.PhoneNumber);
        request.Text.ShouldBe(SmsText.ForFewSessionsLeft(Name, 2));
    }

    [Theory]
    [InlineData(8, 1, true)]
    [InlineData(8, 2, false)]
    [InlineData(9, 2, true)]
    [InlineData(15, 2, true)]
    [InlineData(15, 3, false)]
    [InlineData(16, 3, true)]
    [InlineData(140, 3, true)]
    [InlineData(140, 4, false)]
    public async Task Handle_LowSessionsByPlanSize_EachPlanAtItsOwnRow(int sessions, int left, bool sent)
    {
        // Task 10.17: plans of 5 to 8 sessions at 1 left, up to 15 at 2, bigger at 3.
        await SaveSettingsAsync(settings => settings.Update(
            enabled: true,
            SmsKindSettings.Off,
            new LowSessionsSettings(true, 8, 15, 1, 2, 3, new TimeOnly(10, 0)),
            SmsKindSettings.Off,
            SmsKindSettings.Off,
            OwnerPhone,
            dailyReportEnabled: false,
            dailyReportPhone: null,
            dailyReportSendTime: SmsSettings.DefaultDailyReportSendTime,
            subscriptionReceiptEnabled: false,
            welcomeEnabled: false,
            debtReminder: SmsKindSettings.Off,
            debtReminderMinAmount: null,
            absence: SmsKindSettings.Off,
            broadcastEnabled: false));
        await AddPlanWithSessionsLeftAsync(await AddMemberAsync(), left, sessions: sessions);
        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.LowSessions, sender);

        sender.Requests.Count.ShouldBe(sent ? 1 : 0);
        if (sent)
        {
            sender.Requests[0].Text.ShouldBe(SmsText.ForFewSessionsLeft(Name, left));
        }
    }

    [Fact]
    public async Task Handle_LowSessionsAndExpiringForOnePlan_SendsOneOfEach()
    {
        await TurnOnBothSubscriptionKindsAsync();
        var member = await AddMemberAsync();
        await AddPlanWithSessionsLeftAsync(member, left: 1, daysLeft: 1);
        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.SubscriptionExpiring, sender);
        await RunAsync(NotificationKind.LowSessions, sender);

        sender.Requests.Count.ShouldBe(2);
        (await NotificationsAsync()).Select(notification => notification.Kind)
            .ShouldBe([NotificationKind.SubscriptionExpiring, NotificationKind.LowSessions], ignoreOrder: true);
    }

    // ---- Birthday ----

    [Fact]
    public async Task Handle_Birthday_OnlyActiveMembersHoldingAPlanGetIt()
    {
        // Task 10.8: the SMS tells of the free visit, so only an active member with a plan, active,
        // frozen or bought for later; a used-up plan, a single visit or none does not count.
        await TurnOnAsync(NotificationKind.Birthday, 3);
        var born = JalaliCalendar.ToDate(1370, 7, 16);
        var withPlan = await AddMemberAsync(birthDate: born);
        await AddPlanEndingInAsync(withPlan, daysLeft: 10);
        var frozen = await AddMemberAsync(birthDate: born);
        var frozenPlan = Subscription.CreateMembership(frozen.Id, Sessions, 90_000m, Today.AddDays(-5)).Value;
        frozenPlan.Freeze(Today, maxFreezeDays: 30).IsSuccess.ShouldBeTrue();
        await AddSubscriptionAsync(frozenPlan);
        var queued = await AddMemberAsync(birthDate: born);
        await AddSubscriptionAsync(Subscription.CreateMembership(queued.Id, Sessions, 90_000m, Today.AddDays(5)).Value);
        var deactivated = await AddMemberAsync(birthDate: born, active: false);
        await AddPlanEndingInAsync(deactivated, daysLeft: 10);
        await AddMemberAsync(birthDate: born);
        var singleVisitOnly = await AddMemberAsync(birthDate: born);
        await AddSubscriptionAsync(Subscription.CreateSingleVisit(singleVisitOnly.Id, 1m, Today).Value);
        var usedUp = await AddMemberAsync(birthDate: born);
        await AddPlanWithSessionsLeftAsync(usedUp, left: 0);
        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.Birthday, sender);

        sender.Requests.Select(request => request.Receptor)
            .ShouldBe([withPlan.PhoneNumber, frozen.PhoneNumber, queued.PhoneNumber], ignoreOrder: true);
        sender.Requests[0].Text.ShouldBe(
            $"{SmsText.FirstName(Name)} عزیز، ۰۷/۱۶ روز تولد شماست؛ آن روز مهمان باشگاه پاسارگاد هستید و ورودتان رایگان است. پیشاپیش تولدتان مبارک!\nلغو11");
        (await NotificationsAsync()).ShouldAllBe(notification => notification.JalaliYear == 1405);
    }

    [Fact]
    public async Task Handle_BirthdayToday_SendsNothing()
    {
        await TurnOnAsync(NotificationKind.Birthday, 7);
        var member = await AddMemberAsync(birthDate: JalaliCalendar.ToDate(1370, 7, 14));
        await AddPlanEndingInAsync(member, daysLeft: 10);
        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.Birthday, sender);

        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_BirthdayDaysAhead_OncePerJalaliYearAcrossTheWindow()
    {
        await TurnOnAsync(NotificationKind.Birthday, 3);
        var member = await AddMemberAsync(birthDate: JalaliCalendar.ToDate(1370, 7, 16));
        await AddPlanEndingInAsync(member, daysLeft: 10);
        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.Birthday, sender);
        await RunAsync(NotificationKind.Birthday, sender, at: At(Today.AddDays(1), 10, 0));

        sender.Requests.ShouldHaveSingleItem().Text.ShouldStartWith($"{SmsText.FirstName(Name)} عزیز، ۰۷/۱۶ روز تولد شماست؛");
    }

    [Fact]
    public async Task Handle_BirthdayBornOn30EsfandInACommonYear_GreetedFor29Esfand()
    {
        await TurnOnAsync(NotificationKind.Birthday, 1);
        var day = JalaliCalendar.ToDate(1404, 12, 28);
        var member = await AddMemberAsync(birthDate: JalaliCalendar.ToDate(1379, 12, 30));
        await AddSubscriptionAsync(Subscription.CreateMembership(member.Id, Sessions, 90_000m, day.AddDays(-3)).Value);
        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.Birthday, sender, at: At(day, 10, 0));

        sender.Requests.ShouldHaveSingleItem().Text.ShouldStartWith($"{SmsText.FirstName(Name)} عزیز، ۱۲/۲۹ روز تولد شماست؛");
    }

    // ---- Cheques and instalments ----

    [Fact]
    public async Task Handle_PayableDue_OneSmsForEachChequeAndInstalmentToTheOwner()
    {
        await TurnOnAsync(NotificationKind.PayableDue, 3);
        var userId = await OwnerIdAsync();
        await AddPayableAsync(Cheque(userId, Today.AddDays(3)));
        await AddPayableAsync(Installment(userId, Today));
        await AddPayableAsync(Cheque(userId, Today.AddDays(4)));
        await AddPayableAsync(Cheque(userId, Today.AddDays(-1)));
        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.PayableDue, sender);

        sender.Requests.Count.ShouldBe(2);
        sender.Requests.ShouldAllBe(request => request.Receptor == OwnerPhone);
        sender.Requests[0].Text.ShouldBe("یادآوری قسط: ۲٬۰۰۰٬۰۰۰ تومان، سررسید ۱۴۰۵/۰۷/۱۴، به بانک ملت\nلغو11");
        sender.Requests[1].Text.ShouldBe("یادآوری چک: ۱۲٬۵۰۰٬۰۰۰ تومان، سررسید ۱۴۰۵/۰۷/۱۷، به فروشگاه تجهیزات ورزشی\nلغو11");
    }

    [Fact]
    public async Task Handle_PayableDue_PaidAndCancelledAreLeftOut()
    {
        await TurnOnAsync(NotificationKind.PayableDue, 3);
        var userId = await OwnerIdAsync();

        var paid = Installment(userId, Today.AddDays(1));
        var expense = paid.MarkPaid(Today, At(Today, 9, 0), userId).Value;
        var cancelled = Cheque(userId, Today.AddDays(1));
        cancelled.Cancel("باطل شد", At(Today, 9, 0), userId).IsSuccess.ShouldBeTrue();
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Payables.AddRange(paid, cancelled);
            db.Expenses.Add(expense);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sender = new ScriptedSmsSender();

        await RunAsync(NotificationKind.PayableDue, sender);

        sender.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Handle_PayableDue_DateChangedAfterItsSms_NoSecondSms()
    {
        await TurnOnAsync(NotificationKind.PayableDue, 3);
        var cheque = Cheque(await OwnerIdAsync(), Today.AddDays(1));
        await AddPayableAsync(cheque);
        var sender = new ScriptedSmsSender();
        await RunAsync(NotificationKind.PayableDue, sender);

        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stored = await db.Payables.SingleAsync(TestContext.Current.CancellationToken);
            stored.Update(
                stored.Kind, stored.Amount, Today.AddDays(3), stored.Payee, stored.Description, stored.CategoryId, null, null)
                .IsSuccess.ShouldBeTrue();
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await RunAsync(NotificationKind.PayableDue, sender, at: At(Today.AddDays(1), 10, 0));

        sender.Requests.Count.ShouldBe(1);
    }

    // ---- How each outcome is kept ----

    [Fact]
    public async Task Handle_RetryableThenAccepted_IsSentOnTheSecondTry()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        var sender = new ScriptedSmsSender(Retryable);

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        result.Sent.ShouldBe(1);
        var stored = (await NotificationsAsync()).ShouldHaveSingleItem();
        stored.Status.ShouldBe(NotificationStatus.Sent);
        stored.Attempts.ShouldBe(2);
        stored.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public async Task Handle_RetryableEveryTime_IsFailedAfterTheLastTry()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        var sender = new ScriptedSmsSender(Retryable, Retryable, Retryable, Retryable);

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        result.Failed.ShouldBe(1);
        sender.Requests.Count.ShouldBe(3);
        var stored = (await NotificationsAsync()).ShouldHaveSingleItem();
        stored.Status.ShouldBe(NotificationStatus.Failed);
        stored.Attempts.ShouldBe(3);
        stored.ErrorCode.ShouldBe(409);
    }

    [Fact]
    public async Task Handle_RetryableWhileOthersWait_RetriesAfterTheOthers()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        var first = await AddMemberAsync();
        await AddPlanEndingInAsync(first, daysLeft: 1);
        var second = await AddMemberAsync();
        await AddPlanEndingInAsync(second, daysLeft: 2);
        var sender = new ScriptedSmsSender(Retryable);

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        result.Sent.ShouldBe(2);
        sender.Requests.Select(request => request.Receptor).ShouldBe([first.PhoneNumber, second.PhoneNumber, first.PhoneNumber]);
    }

    [Fact]
    public async Task Handle_Retryable_WaitsTheDelayOnTheGymClock()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        var sender = new ScriptedSmsSender(Retryable);
        var time = new FakeTimeProvider(At(Today, 10, 0));

        var run = RunAsync(NotificationKind.SubscriptionExpiring, sender, time, RealWaits);
        while (!run.IsCompleted)
        {
            // Nothing moves the fake clock but this loop, so the run can only be waiting on it.
            await Task.Delay(20, TestContext.Current.CancellationToken);
            time.Advance(TimeSpan.FromSeconds(15));
        }

        (await run).Sent.ShouldBe(1);
        var stored = (await NotificationsAsync()).ShouldHaveSingleItem();
        stored.Attempts.ShouldBe(2);
        stored.SentAt.ShouldNotBeNull().ShouldBeGreaterThanOrEqualTo(At(Today, 10, 1));
    }

    [Fact]
    public async Task Handle_RetryWouldFallAfter2200_IsFailedWithoutWaiting()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        var sender = new ScriptedSmsSender(Retryable);
        var time = new FakeTimeProvider(At(Today, 21, 59, 30));

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender, time, RealWaits);

        result.Failed.ShouldBe(1);
        sender.Requests.Count.ShouldBe(1);
        var stored = (await NotificationsAsync()).ShouldHaveSingleItem();
        stored.Status.ShouldBe(NotificationStatus.Failed);
        stored.Attempts.ShouldBe(1);
        stored.ErrorCode.ShouldBe(409);
    }

    [Fact]
    public async Task Handle_PermanentFailure_IsFailedAtOnceWithItsCode()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.PermanentFailure, ErrorCode: 424));

        await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        sender.Requests.Count.ShouldBe(1);
        var stored = (await NotificationsAsync()).ShouldHaveSingleItem();
        stored.Status.ShouldBe(NotificationStatus.Failed);
        stored.ErrorCode.ShouldBe(424);
    }

    [Fact]
    public async Task Handle_NoAnswer_IsUnknownAndNeverRetried()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.Unknown));

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender);
        await RunAsync(NotificationKind.SubscriptionExpiring, sender, at: At(Today.AddDays(1), 10, 0));

        result.Unknown.ShouldBe(1);
        sender.Requests.Count.ShouldBe(1);
        (await NotificationsAsync()).ShouldHaveSingleItem().Status.ShouldBe(NotificationStatus.Unknown);
    }

    [Fact]
    public async Task Handle_CreditUsedUp_StopsTheRunAndWritesNoMore()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 2);
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.CreditExhausted, ErrorCode: 418));

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        result.CreditExhausted.ShouldBeTrue();
        sender.Requests.Count.ShouldBe(1);
        var stored = (await NotificationsAsync()).ShouldHaveSingleItem();
        stored.Status.ShouldBe(NotificationStatus.Failed);
        stored.ErrorCode.ShouldBe(418);
    }

    [Fact]
    public async Task Handle_CreditUsedUp_NextRunReachesTheOnesLeft()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        var left = await AddMemberAsync();
        await AddPlanEndingInAsync(left, daysLeft: 2);
        var sender = new ScriptedSmsSender(new SmsSendResult(SmsSendOutcome.CreditExhausted, ErrorCode: 418));
        await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        await RunAsync(NotificationKind.SubscriptionExpiring, sender, at: At(Today.AddDays(1), 10, 0));

        sender.Requests[^1].Receptor.ShouldBe(left.PhoneNumber);
        sender.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Handle_CreditUsedUpDuringRetries_GivesUpTheWaiting()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 1);
        await AddPlanEndingInAsync(await AddMemberAsync(), daysLeft: 2);
        var sender = new ScriptedSmsSender(
            Retryable, Retryable, new SmsSendResult(SmsSendOutcome.CreditExhausted, ErrorCode: 418));

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        result.CreditExhausted.ShouldBeTrue();
        result.Failed.ShouldBe(2);
        sender.Requests.Count.ShouldBe(3);
        (await NotificationsAsync()).ShouldAllBe(notification => notification.Status == NotificationStatus.Failed);
    }

    [Fact]
    public async Task Handle_PendingLeftByAStoppedRun_IsUnknownAndNotSentAgain()
    {
        await TurnOnAsync(NotificationKind.SubscriptionExpiring, 3);
        var member = await AddMemberAsync();
        var plan = await AddPlanEndingInAsync(member, daysLeft: 1);
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Notifications.Add(Notification.ForSubscriptionExpiring(
                member.Id, plan.Id, member.PhoneNumber, SmsText.ForRunningOut(Name, plan.EndDate, plan.RemainingSessions)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var sender = new ScriptedSmsSender();

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, sender);

        result.Interrupted.ShouldBe(1);
        sender.Requests.ShouldBeEmpty();
        var stored = (await NotificationsAsync()).ShouldHaveSingleItem();
        stored.Status.ShouldBe(NotificationStatus.Unknown);
        stored.Attempts.ShouldBe(0);
    }

    [Fact]
    public async Task Handle_PendingOfAnotherKind_IsLeftAlone()
    {
        await TurnOnBothSubscriptionKindsAsync();
        var member = await AddMemberAsync();
        var plan = await AddPlanWithSessionsLeftAsync(member, left: 1, daysLeft: 20);
        await using (var scope = Fixture.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Notifications.Add(Notification.ForLowSessions(
                member.Id, plan.Id, member.PhoneNumber, SmsText.ForFewSessionsLeft(Name, 1)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await RunAsync(NotificationKind.SubscriptionExpiring, new ScriptedSmsSender());

        result.Interrupted.ShouldBe(0);
        (await NotificationsAsync()).ShouldHaveSingleItem().Status.ShouldBe(NotificationStatus.Pending);
    }

    // ---- Helpers ----

    private Task<SmsRunResult> RunAsync(NotificationKind kind, ScriptedSmsSender sender, DateTimeOffset? at = null) =>
        RunAsync(kind, sender, new FakeTimeProvider(at ?? At(Today, 10, 0)), NoWait);

    private async Task<SmsRunResult> RunAsync(
        NotificationKind kind, ScriptedSmsSender sender, FakeTimeProvider time, SmsRetrySchedule retries)
    {
        var calendar = new GymCalendar(time, Options.Create(new GymCalendarOptions { TimeZone = "Asia/Tehran" }));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var handler = new SendDailySmsHandler(
            db,
            calendar,
            new SmsBatchSender(db, calendar, time, sender, retries),
            scope.ServiceProvider.GetRequiredService<DailyReportCandidate>(),
            scope.ServiceProvider.GetRequiredService<DebtReminderCandidate>(),
            scope.ServiceProvider.GetRequiredService<AbsenceCandidate>(),
            NullLogger<SendDailySmsHandler>.Instance);

        return await handler.Handle(kind, TestContext.Current.CancellationToken);
    }

    /// <summary>Turns on one kind at 10:00 and leaves the others off, as the Owner would on the page.</summary>
    private async Task TurnOnAsync(NotificationKind kind, int threshold, bool allSms = true)
    {
        var on = new SmsKindSettings(Enabled: true, threshold, new TimeOnly(10, 0));

        await SaveSettingsAsync(settings => settings.Update(
            allSms,
            kind == NotificationKind.SubscriptionExpiring ? on : SmsKindSettings.Off,
            kind == NotificationKind.LowSessions
                ? new LowSessionsSettings(true, 8, 15, threshold, threshold, threshold, new TimeOnly(10, 0))
                : LowSessionsSettings.Off,
            kind == NotificationKind.Birthday ? on : SmsKindSettings.Off,
            kind == NotificationKind.PayableDue ? on : SmsKindSettings.Off,
            OwnerPhone,
            dailyReportEnabled: kind == NotificationKind.DailyReport,
            kind == NotificationKind.DailyReport ? OwnerPhone : null,
            dailyReportSendTime: SmsSettings.DefaultDailyReportSendTime,
            subscriptionReceiptEnabled: false,
            welcomeEnabled: false,
            debtReminder: SmsKindSettings.Off,
            debtReminderMinAmount: null,
            absence: kind == NotificationKind.Absence ? on : SmsKindSettings.Off,
            broadcastEnabled: false));
    }

    private Task TurnOnBothSubscriptionKindsAsync() => SaveSettingsAsync(settings => settings.Update(
        enabled: true,
        new SmsKindSettings(true, 3, new TimeOnly(10, 0)),
        new LowSessionsSettings(true, 8, 15, 2, 2, 2, new TimeOnly(10, 0)),
        SmsKindSettings.Off,
        SmsKindSettings.Off,
        OwnerPhone,
        dailyReportEnabled: false,
        dailyReportPhone: null,
        dailyReportSendTime: SmsSettings.DefaultDailyReportSendTime,
        subscriptionReceiptEnabled: false,
        welcomeEnabled: false,
        debtReminder: SmsKindSettings.Off,
        debtReminderMinAmount: null,
        absence: SmsKindSettings.Off,
        broadcastEnabled: false));

    private async Task SaveSettingsAsync(Func<SmsSettings, Result> update)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var settings = await db.SmsSettings.SingleAsync(TestContext.Current.CancellationToken);
        update(settings).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Member> AddMemberAsync(DateOnly? birthDate = null, bool active = true)
    {
        var suffix = Interlocked.Increment(ref _phoneSuffix);
        var member = birthDate is { } born
            ? TestMembers.Seed(Name, $"+98912{suffix:D7}", born)
            : TestMembers.Seed(Name, $"+98912{suffix:D7}");
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

    /// <summary>A plan of 10 sessions, none used, whose last day is <paramref name="daysLeft"/> days from today.</summary>
    private Task<Subscription> AddPlanEndingInAsync(Member member, int daysLeft) =>
        AddSubscriptionAsync(Subscription.CreateMembership(member.Id, Sessions, 90_000m, Today.AddDays(daysLeft - PlanDays + 1)).Value);

    private Task<Subscription> AddPlanWithSessionsLeftAsync(Member member, int left, int daysLeft = 20, int sessions = Sessions)
    {
        var days = Subscription.DurationDaysFor(sessions).Value;
        var plan = Subscription.CreateMembership(member.Id, sessions, 90_000m, Today.AddDays(daysLeft - days + 1)).Value;
        for (var i = 0; i < sessions - left; i++)
        {
            plan.ConsumeSession(Today).IsSuccess.ShouldBeTrue();
        }

        return AddSubscriptionAsync(plan);
    }

    private async Task<Subscription> AddSubscriptionAsync(Subscription subscription)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return subscription;
    }

    private async Task<Guid> OwnerIdAsync() => (await TestUsers.CreateAsync(Fixture, userName: "owner", role: Roles.Owner)).Id;

    private static Payable Cheque(Guid userId, DateOnly dueDate) => Payable.Register(
        PayableKind.Cheque, 12_500_000m, dueDate, "فروشگاه تجهیزات ورزشی", "تردمیل", ExpenseCategory.CafePurchasingId, null, null, userId).Value;

    private static Payable Installment(Guid userId, DateOnly dueDate) => Payable.Register(
        PayableKind.Installment, 2_000_000m, dueDate, "بانک ملت", "وام تجهیزات", ExpenseCategory.CafePurchasingId, 3, 12, userId).Value;

    private async Task AddPayableAsync(Payable payable)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Payables.Add(payable);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<Notification>> NotificationsAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Notifications.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>A moment on <paramref name="day"/> at the given time on Tehran's clock.</summary>
    private static DateTimeOffset At(DateOnly day, int hour, int minute, int second = 0)
    {
        var local = day.ToDateTime(new TimeOnly(hour, minute, second), DateTimeKind.Unspecified);

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, Tehran), TimeSpan.Zero);
    }
}
