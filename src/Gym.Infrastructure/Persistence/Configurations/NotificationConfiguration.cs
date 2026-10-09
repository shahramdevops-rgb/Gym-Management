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
                "kind IN ('SubscriptionExpiring', 'LowSessions', 'Birthday', 'PayableDue')");
            table.HasCheckConstraint(
                "ck_notifications_status",
                "status IN ('Pending', 'Sent', 'Failed', 'Unknown')");
            table.HasCheckConstraint(
                "ck_notifications_delivery",
                "delivery IS NULL OR delivery IN ('Delivered', 'NotDelivered', 'BlockedByReceiver')");
            table.HasCheckConstraint("ck_notifications_recipient_not_blank", "btrim(recipient) <> ''");
            table.HasCheckConstraint("ck_notifications_text_not_blank", "btrim(text) <> ''");
            table.HasCheckConstraint("ck_notifications_attempts_not_negative", "attempts >= 0");
            table.HasCheckConstraint("ck_notifications_cost_not_negative", "cost_rial IS NULL OR cost_rial >= 0");

            // Each kind names its own event and nothing else, so the unique indexes below always see
            // the ids they are built on. The IS NOT NULLs are needed: a check that comes out NULL
            // counts as passed in Postgres.
            table.HasCheckConstraint(
                NotificationConstraints.EventMatchesKind,
                "(kind IN ('SubscriptionExpiring', 'LowSessions') AND member_id IS NOT NULL " +
                "AND subscription_id IS NOT NULL AND payable_id IS NULL AND jalali_year IS NULL) OR " +
                "(kind = 'Birthday' AND member_id IS NOT NULL AND jalali_year IS NOT NULL " +
                "AND subscription_id IS NULL AND payable_id IS NULL) OR " +
                "(kind = 'PayableDue' AND payable_id IS NOT NULL " +
                "AND member_id IS NULL AND subscription_id IS NULL AND jalali_year IS NULL)");

            table.HasCheckConstraint(
                NotificationConstraints.SentHasProviderId,
                "status <> 'Sent' OR (provider_message_id IS NOT NULL AND sent_at IS NOT NULL AND cost_rial IS NOT NULL)");
            table.HasCheckConstraint(
                NotificationConstraints.DeliveryOnlyWhenSent,
                "delivery IS NULL OR status = 'Sent'");
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

        // Each event once (BUSINESS_RULES.md §10). The kind is in the first index because a
        // subscription can get both of its kinds, once each.
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

        builder.Property(notification => notification.Version).IsRowVersion();
    }
}
