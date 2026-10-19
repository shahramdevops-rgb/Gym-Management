using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>sms_broadcasts</c> table: one row per broadcast (BUSINESS_RULES.md §10 <i>The broadcast</i>,
/// task 10.13). Its messages are rows of <c>notifications</c> that name it.
/// </summary>
public sealed class SmsBroadcastConfiguration : IEntityTypeConfiguration<SmsBroadcast>
{
    /// <summary>One broadcast sending at a time, whatever two tabs or two clicks do.</summary>
    public const string OneSending = "ix_sms_broadcasts_one_sending";

    public void Configure(EntityTypeBuilder<SmsBroadcast> builder)
    {
        builder.ToTable("sms_broadcasts", table =>
        {
            table.HasCheckConstraint("ck_sms_broadcasts_text_not_blank", "btrim(text) <> ''");
            table.HasCheckConstraint(
                "ck_sms_broadcasts_audience", "audience IN ('ActiveMembers', 'MembersWithUsablePlan')");
            table.HasCheckConstraint(
                "ck_sms_broadcasts_status", "status IN ('Sending', 'Finished', 'Stopped')");

            // A stopped one says why, and only a stopped one; one still sending has not ended.
            table.HasCheckConstraint(
                "ck_sms_broadcasts_stop_reason",
                "(status = 'Stopped' AND stop_reason IN ('CreditExhausted', 'OutsideSendingHours', 'TurnedOff', 'Error', 'TooLong')) " +
                "OR (status <> 'Stopped' AND stop_reason IS NULL)");
            table.HasCheckConstraint(
                "ck_sms_broadcasts_ended_when_not_sending",
                "(status = 'Sending' AND ended_at IS NULL) OR (status <> 'Sending' AND ended_at IS NOT NULL)");
        });

        builder.Property(broadcast => broadcast.Text).HasMaxLength(SmsBroadcast.TextMaxLength).IsRequired();
        builder.Property(broadcast => broadcast.Audience).HasConversion<string>().HasMaxLength(30);
        builder.Property(broadcast => broadcast.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(broadcast => broadcast.StopReason).HasConversion<string>().HasMaxLength(30);

        // The status is the same in every row the filter keeps, so a second row is a duplicate.
        builder.HasIndex(broadcast => broadcast.Status)
            .IsUnique()
            .HasFilter("status = 'Sending'")
            .HasDatabaseName(OneSending);

        // The list: the latest first.
        builder.HasIndex(broadcast => broadcast.CreatedAt).HasDatabaseName("ix_sms_broadcasts_created_at");

        builder.Property(broadcast => broadcast.Version).IsRowVersion();
    }
}
