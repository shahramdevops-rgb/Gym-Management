using Gym.Api.IntegrationTests.Auth;
using Gym.Api.IntegrationTests.Infrastructure;
using Gym.Application.Notifications;
using Gym.Domain.Expenses;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.Payables;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;
using Gym.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

namespace Gym.Api.IntegrationTests.Notifications;

/// <summary>
/// The database's own copy of "each event is sent once" (BUSINESS_RULES.md §10 <i>The four kinds</i>)
/// and of the message's shape. The unique indexes are tried through EF Core, the way a second run of
/// a job would hit them; the check constraints with raw SQL that goes around the application.
/// </summary>
[Collection(DatabaseCollectionDefinition.Name)]
public sealed class NotificationPersistenceTests(DatabaseFixture fixture) : DatabaseTestBase(fixture)
{
    private const string Phone = "+989121234567";
    private const string OwnerPhone = "+989351112233";

    // ---- Each kind ----

    [Fact]
    public async Task Save_OneOfEachKind_ReadsBackAsWritten()
    {
        var (member, subscription, payable) = await SeedAsync();

        await SaveAsync(
            Notification.ForSubscriptionExpiring(member.Id, subscription.Id, Phone, ExpiringText),
            Notification.ForLowSessions(member.Id, subscription.Id, Phone, LowSessionsText),
            Notification.ForBirthday(member.Id, 1405, Phone, BirthdayText),
            Notification.ForPayableDue(payable.Id, OwnerPhone, ChequeText));

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Notifications.ToListAsync(TestContext.Current.CancellationToken);

        saved.Count.ShouldBe(4);
        saved.ShouldAllBe(notification => notification.Status == NotificationStatus.Pending);

        var payableDue = saved.Single(notification => notification.Kind == NotificationKind.PayableDue);
        payableDue.PayableId.ShouldBe(payable.Id);
        payableDue.Recipient.ShouldBe(OwnerPhone);
        payableDue.Text.ShouldBe(ChequeText);

        var birthday = saved.Single(notification => notification.Kind == NotificationKind.Birthday);
        birthday.MemberId.ShouldBe(member.Id);
        birthday.JalaliYear.ShouldBe(1405);
    }

    [Fact]
    public async Task Save_SentWithItsCostAndDelivery_ReadsBackAsWritten()
    {
        var (member, subscription, _) = await SeedAsync();
        var notification = Notification.ForSubscriptionExpiring(member.Id, subscription.Id, Phone, ExpiringText);
        var now = new DateTimeOffset(2026, 10, 6, 6, 30, 0, TimeSpan.Zero);
        notification.MarkSent(8_792_343_210, 1_350.5m, now);
        notification.RecordDelivery(SmsDelivery.Delivered);

        await SaveAsync(notification);

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.Notifications.SingleAsync(TestContext.Current.CancellationToken);
        saved.Status.ShouldBe(NotificationStatus.Sent);
        saved.ProviderMessageId.ShouldBe(8_792_343_210);
        saved.CostRial.ShouldBe(1_350.5m);
        saved.SentAt.ShouldBe(now);
        saved.Attempts.ShouldBe(1);
        saved.Delivery.ShouldBe(SmsDelivery.Delivered);
    }

    // ---- Each event once ----

    [Fact]
    public async Task Save_SecondExpiringForOneSubscription_RejectedByTheUniqueIndex()
    {
        var (member, subscription, _) = await SeedAsync();
        await SaveAsync(Notification.ForSubscriptionExpiring(member.Id, subscription.Id, Phone, ExpiringText));

        var exception = await SaveRejectedAsync(
            Notification.ForSubscriptionExpiring(member.Id, subscription.Id, Phone, ExpiringText));

        exception.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        exception.ConstraintName.ShouldBe(NotificationConstraints.OnePerSubscriptionAndKind);
    }

    [Fact]
    public async Task Save_SecondLowSessionsForOneSubscription_RejectedByTheUniqueIndex()
    {
        var (member, subscription, _) = await SeedAsync();
        await SaveAsync(Notification.ForLowSessions(member.Id, subscription.Id, Phone, LowSessionsText));

        var exception = await SaveRejectedAsync(
            Notification.ForLowSessions(member.Id, subscription.Id, Phone, LowSessionsText));

        exception.ConstraintName.ShouldBe(NotificationConstraints.OnePerSubscriptionAndKind);
    }

    [Fact]
    public async Task Save_BothSubscriptionKindsForOneSubscription_AreAllowed()
    {
        // Running out of days and running out of sessions are two events, one message each.
        var (member, subscription, _) = await SeedAsync();

        await SaveAsync(
            Notification.ForSubscriptionExpiring(member.Id, subscription.Id, Phone, ExpiringText),
            Notification.ForLowSessions(member.Id, subscription.Id, Phone, LowSessionsText));

        (await CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Save_SecondBirthdayInOneJalaliYear_RejectedByTheUniqueIndex()
    {
        var (member, _, _) = await SeedAsync();
        await SaveAsync(Notification.ForBirthday(member.Id, 1405, Phone, BirthdayText));

        var exception = await SaveRejectedAsync(Notification.ForBirthday(member.Id, 1405, Phone, BirthdayText));

        exception.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        exception.ConstraintName.ShouldBe(NotificationConstraints.OneBirthdayPerMemberAndYear);
    }

    [Fact]
    public async Task Save_BirthdayInTheNextJalaliYear_IsAllowed()
    {
        var (member, _, _) = await SeedAsync();
        await SaveAsync(Notification.ForBirthday(member.Id, 1405, Phone, BirthdayText));

        await SaveAsync(Notification.ForBirthday(member.Id, 1406, Phone, BirthdayText));

        (await CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task Save_SecondPayableDueForOneCheque_RejectedByTheUniqueIndex()
    {
        var (_, _, payable) = await SeedAsync();
        await SaveAsync(Notification.ForPayableDue(payable.Id, OwnerPhone, ChequeText));

        var exception = await SaveRejectedAsync(
            Notification.ForPayableDue(payable.Id, OwnerPhone, ChequeText));

        exception.SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
        exception.ConstraintName.ShouldBe(NotificationConstraints.OnePerPayable);
    }

    // ---- Check constraints ----

    [Fact]
    public async Task Notification_SubscriptionKindWithoutItsSubscription_RejectedByACheckConstraint()
    {
        var id = await SaveExpiringAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE notifications SET subscription_id = NULL WHERE id = '{id}'"));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe(NotificationConstraints.EventMatchesKind);
    }

    [Fact]
    public async Task Notification_BirthdayCarryingASubscription_RejectedByACheckConstraint()
    {
        var id = await SaveExpiringAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE notifications SET kind = 'Birthday', jalali_year = 1405 WHERE id = '{id}'"));

        exception.ConstraintName.ShouldBe(NotificationConstraints.EventMatchesKind);
    }

    [Fact]
    public async Task Notification_PayableDueCarryingAMember_RejectedByACheckConstraint()
    {
        var (member, _, payable) = await SeedAsync();
        var notification = Notification.ForPayableDue(payable.Id, OwnerPhone, ChequeText);
        await SaveAsync(notification);

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE notifications SET member_id = '{member.Id}' WHERE id = '{notification.Id}'"));

        exception.ConstraintName.ShouldBe(NotificationConstraints.EventMatchesKind);
    }

    [Fact]
    public async Task Notification_SentWithoutTheProvidersId_RejectedByACheckConstraint()
    {
        var id = await SaveExpiringAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE notifications SET status = 'Sent', sent_at = now(), cost_rial = 0 WHERE id = '{id}'"));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe(NotificationConstraints.SentHasProviderId);
    }

    [Fact]
    public async Task Notification_DeliveryWhileNotSent_RejectedByACheckConstraint()
    {
        var id = await SaveExpiringAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE notifications SET delivery = 'Delivered' WHERE id = '{id}'"));

        exception.ConstraintName.ShouldBe(NotificationConstraints.DeliveryOnlyWhenSent);
    }

    [Theory]
    [InlineData("BlockedByReceiver")]
    [InlineData("Cancelled")]
    public async Task Notification_RefundedWithACost_RejectedByACheckConstraint(string delivery)
    {
        var id = await SaveExpiringAsync();
        await ExecuteSqlAsync(
            $"UPDATE notifications SET status = 'Sent', provider_message_id = 1, sent_at = now(), cost_rial = 0, delivery = '{delivery}' WHERE id = '{id}'");

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE notifications SET cost_rial = 1350 WHERE id = '{id}'"));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe(NotificationConstraints.RefundedHasNoCost);
    }

    [Fact]
    public async Task Notification_UnknownKind_RejectedByACheckConstraint()
    {
        var id = await SaveExpiringAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE notifications SET kind = 'Promotion' WHERE id = '{id}'"));

        // Both the list of kinds and the event check refuse it; Postgres names whichever it tried first.
        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBeOneOf("ck_notifications_kind", NotificationConstraints.EventMatchesKind);
    }

    [Theory]
    [InlineData("status = 'Lost'", "ck_notifications_status")]
    [InlineData("attempts = -1", "ck_notifications_attempts_not_negative")]
    [InlineData("cost_rial = -1", "ck_notifications_cost_not_negative")]
    [InlineData("text = ' '", "ck_notifications_text_not_blank")]
    public async Task Notification_InvalidColumn_RejectedByACheckConstraint(string assignment, string constraint)
    {
        var id = await SaveExpiringAsync();

        var exception = await Should.ThrowAsync<PostgresException>(
            () => ExecuteSqlAsync($"UPDATE notifications SET {assignment} WHERE id = '{id}'"));

        exception.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        exception.ConstraintName.ShouldBe(constraint);
    }

    // ---- Concurrency ----

    [Fact]
    public async Task Save_TwoOutcomesForOneMessage_SecondRefusedByXmin()
    {
        var id = await SaveExpiringAsync();
        var now = new DateTimeOffset(2026, 10, 6, 6, 30, 0, TimeSpan.Zero);

        await using var firstScope = Fixture.CreateScope();
        await using var secondScope = Fixture.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var firstCopy = await first.Notifications.SingleAsync(n => n.Id == id, TestContext.Current.CancellationToken);
        var secondCopy = await second.Notifications.SingleAsync(n => n.Id == id, TestContext.Current.CancellationToken);

        firstCopy.MarkSent(1, 1_200m, now);
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        secondCopy.MarkFailed(424, now);

        await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => second.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    // ---- Helpers ----

    private const string ExpiringText = "سارا محمدی عزیز، اشتراک شما در باشگاه پاسارگاد ۱۴۰۵/۰۷/۲۰ به پایان می‌رسد.";

    private const string LowSessionsText = "سارا محمدی عزیز، فقط ۲ جلسه از اشتراک شما در باشگاه پاسارگاد مانده است.";

    private const string BirthdayText = "سارا محمدی عزیز، امروز ۱۴۰۵/۰۷/۲۰ روز تولد شماست. تولدتان مبارک! باشگاه پاسارگاد";

    private const string ChequeText = "یادآوری چک: ۱۲٬۵۰۰٬۰۰۰ تومان، سررسید ۱۴۰۵/۰۷/۲۰، به فروشگاه تجهیزات ورزشی";

    /// <summary>A member with a subscription, and a pending cheque: one event of every kind can point at them.</summary>
    private async Task<(Member Member, Subscription Subscription, Payable Payable)> SeedAsync()
    {
        var owner = await TestUsers.CreateAsync(Fixture, userName: "owner", role: Roles.Owner);
        var member = TestMembers.Seed("سارا محمدی", Phone);
        var subscription = Subscription.CreateMembership(member.Id, 10, 90_000m, new DateOnly(2026, 10, 1)).Value;
        var payable = Payable.Register(
            PayableKind.Cheque,
            12_500_000m,
            new DateOnly(2026, 10, 12),
            "فروشگاه تجهیزات ورزشی",
            "تردمیل",
            ExpenseCategory.CafePurchasingId,
            installmentNumber: null,
            installmentCount: null,
            owner.Id).Value;

        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Members.Add(member);
        db.Subscriptions.Add(subscription);
        db.Payables.Add(payable);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (member, subscription, payable);
    }

    private async Task<Guid> SaveExpiringAsync()
    {
        var (member, subscription, _) = await SeedAsync();
        var notification = Notification.ForSubscriptionExpiring(member.Id, subscription.Id, Phone, ExpiringText);
        await SaveAsync(notification);

        return notification.Id;
    }

    private async Task SaveAsync(params Notification[] notifications)
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<PostgresException> SaveRejectedAsync(Notification notification)
    {
        var exception = await Should.ThrowAsync<DbUpdateException>(() => SaveAsync(notification));

        return exception.InnerException.ShouldBeOfType<PostgresException>();
    }

    private async Task<int> CountAsync()
    {
        await using var scope = Fixture.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Notifications.CountAsync(TestContext.Current.CancellationToken);
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new NpgsqlConnection(Fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
