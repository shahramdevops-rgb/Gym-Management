using Gym.Application.Notifications;
using Gym.Domain.Members;
using Gym.Domain.Notifications;
using Gym.Domain.Payables;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>notifications</c> table: one row per SMS (BUSINESS_RULES.md §10). The unique indexes are
/// what make "the same message is never paid for twice" true: the row is written before the SMS is
/// sent, and a second row for the same event cannot be written at all.
/// </summary>
public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications", table =>
        {
            table.HasCheckConstraint(
                "ck_notifications_kind",
                "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue', 'DailyReport', 'SubscriptionReceipt', 'Welcome', 'DebtReminder', 'Absence', 'Broadcast')");
            table.HasCheckConstraint(
                "ck_notifications_status",
                "status IN ('Pending', 'Sent', 'Failed', 'Unknown')");
            table.HasCheckConstraint(
                "ck_notifications_delivery",
                "delivery IS NULL OR delivery IN ('Delivered', 'NotDelivered', 'BlockedByReceiver', 'Cancelled')");
            table.HasCheckConstraint("ck_notifications_recipient_not_blank", "btrim(recipient) <> ''");
            table.HasCheckConstraint("ck_notifications_text_not_blank", "btrim(text) <> ''");
            table.HasCheckConstraint("ck_notifications_attempts_not_negative", "attempts >= 0");
            table.HasCheckConstraint("ck_notifications_cost_not_negative", "cost_rial IS NULL OR cost_rial >= 0");

            // Each kind names its own event and nothing else, so the unique indexes below always see
            // the ids they are built on. The IS NOT NULLs are needed: a check that comes out NULL
            // counts as passed in Postgres.
            table.HasCheckConstraint(
                NotificationConstraints.EventMatchesKind,
                "(kind IN ('SubscriptionExpiring', 'LowSessions', 'SubscriptionReceipt', 'Welcome') AND member_id IS NOT NULL " +
                "AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL " +
                "AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR " +
                "(kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL " +
                "AND subscription_id IS NULL AND payable_id IS NULL AND report_date IS NULL AND debt_since IS NULL " +
                "AND absent_since IS NULL AND broadcast_id IS NULL) OR " +
                "(kind = 'PayableDue' AND payable_id IS NOT NULL " +
                "AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL AND report_date IS NULL " +
                "AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR " +
                "(kind = 'DailyReport' AND report_date IS NOT NULL " +
                "AND member_id IS NULL AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL " +
                "AND debt_since IS NULL AND absent_since IS NULL AND broadcast_id IS NULL) OR " +
                "(kind = 'DebtReminder' AND member_id IS NOT NULL AND debt_since IS NOT NULL " +
                "AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL " +
                "AND absent_since IS NULL AND broadcast_id IS NULL) OR " +
                "(kind = 'Absence' AND member_id IS NOT NULL AND absent_since IS NOT NULL " +
                "AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL " +
                "AND debt_since IS NULL AND broadcast_id IS NULL) OR " +
                "(kind = 'Broadcast' AND member_id IS NOT NULL AND broadcast_id IS NOT NULL " +
                "AND subscription_id IS NULL AND payable_id IS NULL AND jalali_year IS NULL AND report_date IS NULL " +
                "AND debt_since IS NULL AND absent_since IS NULL)");

            table.HasCheckConstraint(
                NotificationConstraints.SentHasProviderId,
                "status <> 'Sent' OR (provider_message_id IS NOT NULL AND sent_at IS NOT NULL AND cost_rial IS NOT NULL)");
            table.HasCheckConstraint(
                NotificationConstraints.DeliveryOnlyWhenSent,
                "delivery IS NULL OR status = 'Sent'");

            // §10 Sending: the provider gives the cost back for a blocked or cancelled message.
            table.HasCheckConstraint(
                NotificationConstraints.RefundedHasNoCost,
                "delivery IS NULL OR delivery NOT IN ('BlockedByReceiver', 'Cancelled') OR cost_rial = 0");

            // §10 Sending (task 10.16): a cancelled message is resent by itself at most this many times.
            table.HasCheckConstraint(
                NotificationConstraints.AutoResendsInRange,
                $"auto_resends BETWEEN 0 AND {Notification.MaxAutoResends}");
        });

        builder.Property(notification => notification.Kind).HasConversion<string>().HasMaxLength(30);
        builder.Property(notification => notification.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(notification => notification.Delivery).HasConversion<string>().HasMaxLength(20);

        builder.Property(notification => notification.Recipient).HasMaxLength(Notification.RecipientMaxLength).IsRequired();
        builder.Property(notification => notification.Text).HasMaxLength(Notification.TextMaxLength).IsRequired();

        // Rial as the provider reports it, in a money column like every other amount.
        builder.Property(notification => notification.CostRial).HasPrecision(18, 2);

        // Restrict: members, subscriptions and payables are never deleted, and a message about one
        // must not disappear with it.
        builder.HasOne<Member>().WithMany().HasForeignKey(notification => notification.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subscription>().WithMany().HasForeignKey(notification => notification.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Payable>().WithMany().HasForeignKey(notification => notification.PayableId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<SmsBroadcast>().WithMany().HasForeignKey(notification => notification.BroadcastId).OnDelete(DeleteBehavior.Restrict);

        // Each event once (BUSINESS_RULES.md §10). The kind is in the first index because a
        // subscription can get each of its kinds (running out, few sessions, receipt, welcome) once.
        builder.HasIndex(notification => new { notification.SubscriptionId, notification.Kind })
            .IsUnique()
            .HasFilter("subscription_id IS NOT NULL")
            .HasDatabaseName(NotificationConstraints.OnePerSubscriptionAndKind);

        builder.HasIndex(notification => new { notification.MemberId, notification.JalaliYear })
            .IsUnique()
            .HasFilter("kind = 'Birthday'")
            .HasDatabaseName(NotificationConstraints.OneBirthdayPerMemberAndYear);

        builder.HasIndex(notification => notification.PayableId)
            .IsUnique()
            .HasFilter("payable_id IS NOT NULL")
            .HasDatabaseName(NotificationConstraints.OnePerPayable);

        builder.HasIndex(notification => notification.ReportDate)
            .IsUnique()
            .HasFilter("kind = 'DailyReport'")
            .HasDatabaseName(NotificationConstraints.OneDailyReportPerDay);

        // The welcome names its plan, but it is once per member: a first plan cancelled after its
        // welcome went out does not earn the next plan a second one.
        builder.HasIndex(notification => notification.MemberId)
            .IsUnique()
            .HasFilter("kind = 'Welcome'")
            .HasDatabaseName(NotificationConstraints.OneWelcomePerMember);

        // Once per debt (task 10.11): a debt is named by its member and the day its oldest unpaid
        // sale was recorded. A debt that builds after everything before was paid begins later.
        builder.HasIndex(notification => new { notification.MemberId, notification.DebtSince })
            .IsUnique()
            .HasFilter("kind = 'DebtReminder'")
            .HasDatabaseName(NotificationConstraints.OneDebtReminderPerMemberAndDebt);

        // Once per absence (task 10.12): an absence is named by its member and the day it counts
        // from (the last visit, or the plan's start). Coming again starts a later one.
        builder.HasIndex(notification => new { notification.MemberId, notification.AbsentSince })
            .IsUnique()
            .HasFilter("kind = 'Absence'")
            .HasDatabaseName(NotificationConstraints.OneAbsencePerMemberAndAbsence);

        // Once per member and broadcast (task 10.13): a job made again after a restart, or a
        // broadcast continued, cannot write to anyone a second time.
        builder.HasIndex(notification => new { notification.BroadcastId, notification.MemberId })
            .IsUnique()
            .HasFilter("broadcast_id IS NOT NULL")
            .HasDatabaseName(NotificationConstraints.OnePerBroadcastAndMember);

        builder.Property(notification => notification.Version).IsRowVersion();
    }
}
