using Gym.Domain.Notifications;

namespace Gym.Domain.Tests.Notifications;

/// <summary>BUSINESS_RULES.md §10 <i>SMS settings</i>.</summary>
public sealed class SmsSettingsTests
{
    private const string Phone = "+989121234567";
    private const string OtherPhone = "+989351234567";
    private static readonly TimeOnly Ten = new(10, 0);

    // ---- The seeded row ----

    [Fact]
    public void NewSettings_BeforeTheOwnerFillsThem_AreEmptyAndOff()
    {
        var settings = Empty();

        settings.Enabled.ShouldBeFalse();
        settings.OwnerPhone.ShouldBeNull();
        foreach (var kind in Enum.GetValues<NotificationKind>().Where(kind => kind != NotificationKind.DailyReport))
        {
            settings.For(kind).ShouldBe(SmsKindSettings.Off);
        }

        settings.DailyReportEnabled.ShouldBeFalse();
        settings.DailyReportPhone.ShouldBeNull();
    }

    // ---- Saving ----

    [Fact]
    public void Update_EveryKindFilledAndOn_SavesEachKindInItsOwnColumns()
    {
        var settings = Empty();

        var result = settings.Update(
            enabled: true,
            subscriptionExpiring: new(true, 7, new TimeOnly(9, 0)),
            lowSessions: new(true, 2, new TimeOnly(9, 15)),
            birthday: new(true, 0, new TimeOnly(10, 30)),
            payableDue: new(true, 3, new TimeOnly(21, 45)),
            ownerPhone: Phone,
            dailyReportEnabled: true,
            dailyReportPhone: OtherPhone);

        result.IsSuccess.ShouldBeTrue();
        settings.Enabled.ShouldBeTrue();
        settings.For(NotificationKind.SubscriptionExpiring).ShouldBe(new SmsKindSettings(true, 7, new TimeOnly(9, 0)));
        settings.For(NotificationKind.LowSessions).ShouldBe(new SmsKindSettings(true, 2, new TimeOnly(9, 15)));
        settings.For(NotificationKind.Birthday).ShouldBe(new SmsKindSettings(true, 0, new TimeOnly(10, 30)));
        settings.For(NotificationKind.PayableDue).ShouldBe(new SmsKindSettings(true, 3, new TimeOnly(21, 45)));
        settings.OwnerPhone.ShouldBe(Phone);
        settings.DailyReportEnabled.ShouldBeTrue();
        settings.DailyReportPhone.ShouldBe(OtherPhone);
    }

    [Fact]
    public void Update_KindOffAndHalfFilled_Succeeds()
    {
        var settings = Empty();

        var result = Save(settings, birthday: new(false, 3, null));

        result.IsSuccess.ShouldBeTrue();
        settings.BirthdayDaysBefore.ShouldBe(3);
        settings.BirthdaySendTime.ShouldBeNull();
    }

    [Fact]
    public void Update_KindOff_CanBeEmptiedAgain()
    {
        var settings = Empty();
        Save(settings, birthday: new(true, 3, Ten)).IsSuccess.ShouldBeTrue();

        Save(settings, birthday: SmsKindSettings.Off).IsSuccess.ShouldBeTrue();

        settings.For(NotificationKind.Birthday).ShouldBe(SmsKindSettings.Off);
    }

    [Fact]
    public void Update_EverySmsOffWithAKindOn_KeepsBoth()
    {
        var settings = Empty();

        Save(settings, enabled: false, birthday: new(true, 3, Ten)).IsSuccess.ShouldBeTrue();

        settings.Enabled.ShouldBeFalse();
        settings.BirthdayEnabled.ShouldBeTrue();
    }

    [Fact]
    public void Update_Refused_ChangesNothing()
    {
        var settings = Empty();
        Save(settings, birthday: new(true, 3, Ten)).IsSuccess.ShouldBeTrue();

        var result = Save(settings, enabled: true, birthday: new(true, 8, Ten));

        result.Error.ShouldBe(SmsSettingsErrors.BirthdayDaysOutOfRange);
        settings.Enabled.ShouldBeFalse();
        settings.BirthdayDaysBefore.ShouldBe(3);
    }

    [Fact]
    public void Update_PhoneNotNormalized_Throws() =>
        Should.Throw<ArgumentException>(() => Save(Empty(), ownerPhone: "09121234567"));

    // ---- The numbers' ranges ----

    [Theory]
    [InlineData(NotificationKind.SubscriptionExpiring, 1)]
    [InlineData(NotificationKind.SubscriptionExpiring, 30)]
    [InlineData(NotificationKind.LowSessions, 1)]
    [InlineData(NotificationKind.LowSessions, 10)]
    [InlineData(NotificationKind.Birthday, 0)]
    [InlineData(NotificationKind.Birthday, 7)]
    [InlineData(NotificationKind.PayableDue, 0)]
    [InlineData(NotificationKind.PayableDue, 30)]
    public void CheckThreshold_AtTheEdgeOfTheRange_Passes(NotificationKind kind, int threshold) =>
        SmsSettings.CheckThreshold(kind, threshold).ShouldBeNull();

    [Theory]
    [InlineData(NotificationKind.SubscriptionExpiring, 0)]
    [InlineData(NotificationKind.SubscriptionExpiring, 31)]
    [InlineData(NotificationKind.LowSessions, 0)]
    [InlineData(NotificationKind.LowSessions, 11)]
    [InlineData(NotificationKind.Birthday, -1)]
    [InlineData(NotificationKind.Birthday, 8)]
    [InlineData(NotificationKind.PayableDue, -1)]
    [InlineData(NotificationKind.PayableDue, 31)]
    public void CheckThreshold_JustOutsideTheRange_FailsWithTheKindsError(NotificationKind kind, int threshold)
    {
        var expected = kind switch
        {
            NotificationKind.SubscriptionExpiring => SmsSettingsErrors.SubscriptionExpiringDaysOutOfRange,
            NotificationKind.LowSessions => SmsSettingsErrors.LowSessionsOutOfRange,
            NotificationKind.Birthday => SmsSettingsErrors.BirthdayDaysOutOfRange,
            _ => SmsSettingsErrors.PayableDueDaysOutOfRange,
        };

        SmsSettings.CheckThreshold(kind, threshold).ShouldBe(expected);
    }

    [Fact]
    public void CheckThreshold_Empty_Passes() =>
        SmsSettings.CheckThreshold(NotificationKind.SubscriptionExpiring, null).ShouldBeNull();

    // ---- Send times ----

    [Theory]
    [InlineData(8, 0)]
    [InlineData(8, 15)]
    [InlineData(13, 30)]
    [InlineData(21, 45)]
    [InlineData(22, 0)]
    public void CheckSendTime_QuarterHourInTheWindow_Passes(int hour, int minute) =>
        SmsSettings.CheckSendTime(new TimeOnly(hour, minute)).ShouldBeNull();

    [Theory]
    [InlineData(7, 45, 0)]
    [InlineData(7, 59, 0)]
    [InlineData(22, 15, 0)]
    [InlineData(22, 1, 0)]
    [InlineData(0, 0, 0)]
    [InlineData(10, 10, 0)]
    [InlineData(10, 0, 30)]
    public void CheckSendTime_OutsideTheWindowOrOffTheQuarter_FailsWithSendTimeOutOfRange(int hour, int minute, int second) =>
        SmsSettings.CheckSendTime(new TimeOnly(hour, minute, second)).ShouldBe(SmsSettingsErrors.SendTimeOutOfRange);

    [Fact]
    public void CheckSendTime_Empty_Passes() =>
        SmsSettings.CheckSendTime(null).ShouldBeNull();

    // ---- Sending hours (task 10.3) ----

    [Theory]
    [InlineData(8, 0, 0, true)]
    [InlineData(22, 0, 0, true)]
    [InlineData(14, 7, 30, true)]
    [InlineData(7, 59, 59, false)]
    [InlineData(22, 0, 1, false)]
    [InlineData(0, 0, 0, false)]
    public void IsWithinSendingHours_TimeOfDay_IsBetween0800And2200Included(int hour, int minute, int second, bool within) =>
        SmsSettings.IsWithinSendingHours(new TimeOnly(hour, minute, second)).ShouldBe(within);

    // ---- On means filled ----

    [Theory]
    [InlineData(null, 10)]
    [InlineData(3, null)]
    public void Update_KindOnWithAFieldEmpty_FailsWithSettingsIncomplete(int? days, int? hour)
    {
        TimeOnly? sendTime = hour is null ? null : new TimeOnly(hour.Value, 0);

        Save(Empty(), birthday: new(true, days, sendTime)).Error.ShouldBe(SmsSettingsErrors.SettingsIncomplete);
    }

    [Fact]
    public void Update_ChequesOnWithoutTheOwnersNumber_FailsWithSettingsIncomplete() =>
        Save(Empty(), payableDue: new(true, 3, Ten), ownerPhone: null)
            .Error.ShouldBe(SmsSettingsErrors.SettingsIncomplete);

    [Fact]
    public void Update_ChequesOffWithoutTheOwnersNumber_Succeeds() =>
        Save(Empty(), payableDue: new(false, 3, Ten), ownerPhone: null).IsSuccess.ShouldBeTrue();

    [Fact]
    public void Update_TheOwnersNumberWithChequesOff_IsKept()
    {
        var settings = Empty();

        Save(settings, ownerPhone: Phone).IsSuccess.ShouldBeTrue();

        settings.OwnerPhone.ShouldBe(Phone);
    }

    // ---- The daily report (task 10.7) ----

    [Fact]
    public void Update_DailyReportOnWithoutItsNumber_IsRefused()
    {
        var settings = Empty();

        var result = Save(settings, enabled: true, ownerPhone: Phone, dailyReportEnabled: true);

        result.Error.ShouldBe(SmsSettingsErrors.SettingsIncomplete);
        settings.DailyReportEnabled.ShouldBeFalse();
        settings.OwnerPhone.ShouldBeNull();
    }

    [Fact]
    public void Update_DailyReportOffWithANumber_KeepsTheNumber()
    {
        var settings = Empty();

        Save(settings, dailyReportPhone: OtherPhone).IsSuccess.ShouldBeTrue();

        settings.DailyReportEnabled.ShouldBeFalse();
        settings.DailyReportPhone.ShouldBe(OtherPhone);
    }

    [Fact]
    public void Update_DailyReportNumberEmptied_ClearsIt()
    {
        var settings = Empty();
        Save(settings, dailyReportPhone: OtherPhone);

        Save(settings, dailyReportPhone: "  ").IsSuccess.ShouldBeTrue();

        settings.DailyReportPhone.ShouldBeNull();
    }

    [Fact]
    public void Update_DailyReportNumberNotNormalized_Throws()
    {
        Should.Throw<ArgumentException>(() => Save(Empty(), dailyReportPhone: "09121234567"));
    }

    [Fact]
    public void For_DailyReport_HasNoNumberAndTheFixedTime()
    {
        var settings = Empty();
        Save(settings, dailyReportEnabled: true, dailyReportPhone: OtherPhone);

        settings.For(NotificationKind.DailyReport).ShouldBe(new SmsKindSettings(true, null, new TimeOnly(22, 45)));
    }

    [Theory]
    [InlineData(NotificationKind.DailyReport, 8, 0, true)]
    [InlineData(NotificationKind.DailyReport, 22, 45, true)]
    [InlineData(NotificationKind.DailyReport, 23, 59, true)]
    [InlineData(NotificationKind.DailyReport, 7, 59, false)]
    [InlineData(NotificationKind.PayableDue, 22, 0, true)]
    [InlineData(NotificationKind.PayableDue, 22, 1, false)]
    [InlineData(NotificationKind.Birthday, 22, 45, false)]
    public void IsWithinSendingHours_ByKind_FollowsItsOwnHours(NotificationKind kind, int hour, int minute, bool expected)
    {
        SmsSettings.IsWithinSendingHours(kind, new TimeOnly(hour, minute)).ShouldBe(expected);
    }

    [Fact]
    public void IsWithinSendingHours_DailyReportLastMomentOfTheDay_IsAllowed()
    {
        SmsSettings.IsWithinSendingHours(NotificationKind.DailyReport, TimeOnly.MaxValue).ShouldBeTrue();
    }

    // ---- Helpers ----

    private static Domain.Common.Result Save(
        SmsSettings settings,
        bool enabled = false,
        SmsKindSettings? subscriptionExpiring = null,
        SmsKindSettings? lowSessions = null,
        SmsKindSettings? birthday = null,
        SmsKindSettings? payableDue = null,
        string? ownerPhone = null,
        bool dailyReportEnabled = false,
        string? dailyReportPhone = null) =>
        settings.Update(
            enabled,
            subscriptionExpiring ?? SmsKindSettings.Off,
            lowSessions ?? SmsKindSettings.Off,
            birthday ?? SmsKindSettings.Off,
            payableDue ?? SmsKindSettings.Off,
            ownerPhone,
            dailyReportEnabled,
            dailyReportPhone);

    /// <summary>
    /// The one row the migration seeds. There is no public constructor, on purpose: nothing in the
    /// app ever creates the settings, so the test builds them the way EF Core does.
    /// </summary>
    private static SmsSettings Empty() => (SmsSettings)Activator.CreateInstance(typeof(SmsSettings), nonPublic: true)!;
}
