using System.Globalization;

using Gym.Domain.Notifications;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>sms_settings</c> table: one row, seeded by the migration with everything empty and off
/// (BUSINESS_RULES.md §10 <i>SMS settings</i>). The rules live in <see cref="SmsSettings"/>; the
/// database repeats every one it can check, "on means filled" included.
/// </summary>
public sealed class SmsSettingsConfiguration : IEntityTypeConfiguration<SmsSettings>
{
    // E.164 for an Iranian mobile, what IPhoneNormalizer makes of one: +989 and nine digits.
    private const int PhoneMaxLength = 13;

    // The kinds' column prefixes, as snake_case names them.
    private static readonly string[] Kinds = ["subscription_expiring", "low_sessions", "birthday", "payable_due"];

    public void Configure(EntityTypeBuilder<SmsSettings> builder)
    {
        builder.ToTable("sms_settings", table =>
        {
            // One row, ever: the id is fixed, so a second row cannot be inserted even by hand.
            table.HasCheckConstraint("ck_sms_settings_single_row", $"id = '{SmsSettings.TheId}'");

            table.HasCheckConstraint("ck_sms_settings_subscription_expiring_days", Range("subscription_expiring_days_before", NotificationKind.SubscriptionExpiring));
            table.HasCheckConstraint("ck_sms_settings_low_sessions_threshold", Range("low_sessions_threshold", NotificationKind.LowSessions));
            table.HasCheckConstraint("ck_sms_settings_birthday_days", Range("birthday_days_before", NotificationKind.Birthday));
            table.HasCheckConstraint("ck_sms_settings_payable_due_days", Range("payable_due_days_before", NotificationKind.PayableDue));

            // 08:00 to 22:00, both included, on a quarter hour with no seconds.
            table.HasCheckConstraint(
                "ck_sms_settings_send_times",
                string.Join(" AND ", Kinds.Select(kind => SendTime($"{kind}_send_time"))));

            // Kavenegar's rule: English letters and digits only.
            table.HasCheckConstraint(
                "ck_sms_settings_template_names",
                string.Join(" AND ", Kinds.Select(kind => $"({kind}_template_name IS NULL OR {kind}_template_name ~ '^[A-Za-z0-9]+$')")));

            table.HasCheckConstraint("ck_sms_settings_owner_phone", "owner_phone IS NULL OR owner_phone ~ '^\\+989[0-9]{9}$'");

            // On means filled (§10): a kind cannot be on with an empty field.
            table.HasCheckConstraint("ck_sms_settings_subscription_expiring_on_means_filled", OnMeansFilled("subscription_expiring", "subscription_expiring_days_before"));
            table.HasCheckConstraint("ck_sms_settings_low_sessions_on_means_filled", OnMeansFilled("low_sessions", "low_sessions_threshold"));
            table.HasCheckConstraint("ck_sms_settings_birthday_on_means_filled", OnMeansFilled("birthday", "birthday_days_before"));
            table.HasCheckConstraint(
                "ck_sms_settings_payable_due_on_means_filled",
                OnMeansFilled("payable_due", "payable_due_days_before", "owner_phone IS NOT NULL"));
        });

        builder.Property(settings => settings.SubscriptionExpiringTemplateName).HasMaxLength(SmsSettings.TemplateNameMaxLength);
        builder.Property(settings => settings.LowSessionsTemplateName).HasMaxLength(SmsSettings.TemplateNameMaxLength);
        builder.Property(settings => settings.BirthdayTemplateName).HasMaxLength(SmsSettings.TemplateNameMaxLength);
        builder.Property(settings => settings.PayableDueTemplateName).HasMaxLength(SmsSettings.TemplateNameMaxLength);
        builder.Property(settings => settings.OwnerPhone).HasMaxLength(PhoneMaxLength);

        builder.Property(settings => settings.Version).IsRowVersion();

        // An anonymous object, because every setter on the entity is private. Everything starts
        // empty and off: nothing is sent before the Owner decides (§10).
        builder.HasData(new
        {
            Id = SmsSettings.TheId,
            CreatedAt = new DateTimeOffset(2026, 10, 6, 0, 0, 0, TimeSpan.Zero),
            Enabled = false,
            SubscriptionExpiringEnabled = false,
            LowSessionsEnabled = false,
            BirthdayEnabled = false,
            PayableDueEnabled = false,
        });
    }

    private static string Range(string column, NotificationKind kind)
    {
        var (min, max) = SmsSettings.ThresholdRange(kind);

        return $"{column} IS NULL OR {column} BETWEEN {min} AND {max}";
    }

    private static string SendTime(string column) =>
        $"({column} IS NULL OR ({column} BETWEEN '{Clock(SmsSettings.EarliestSendTime)}' AND '{Clock(SmsSettings.LatestSendTime)}'"
        + $" AND EXTRACT(MINUTE FROM {column}) IN (0, 15, 30, 45) AND EXTRACT(SECOND FROM {column}) = 0))";

    private static string Clock(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string OnMeansFilled(string kind, string thresholdColumn, string? extra = null) =>
        $"NOT {kind}_enabled OR ({thresholdColumn} IS NOT NULL AND {kind}_send_time IS NOT NULL"
        + $" AND {kind}_template_name IS NOT NULL{(extra is null ? string.Empty : " AND " + extra)})";
}
