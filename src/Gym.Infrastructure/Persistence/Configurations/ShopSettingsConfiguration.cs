using Gym.Domain.Shop;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>shop_settings</c> table: one row, seeded by the migration with every contact way empty
/// (BUSINESS_RULES.md §16 <i>The shop admin</i>). The shapes live in <see cref="ShopContactRules"/>;
/// the database repeats each one.
/// </summary>
public sealed class ShopSettingsConfiguration : IEntityTypeConfiguration<ShopSettings>
{
    // The columns by the shape they hold, as snake_case names them.
    private static readonly string[] Mobiles = ["mobile_phone", "whats_app_phone"];
    private static readonly string[] Handles = ["telegram", "rubika", "eitaa", "bale", "instagram"];

    public void Configure(EntityTypeBuilder<ShopSettings> builder)
    {
        builder.ToTable("shop_settings", table =>
        {
            // One row, ever: the id is fixed, so a second row cannot be inserted even by hand.
            table.HasCheckConstraint("ck_shop_settings_single_row", $"id = '{ShopSettings.TheId}'");

            table.HasCheckConstraint("ck_shop_settings_mobiles", Matches(Mobiles, ShopContactRules.MobilePattern));
            table.HasCheckConstraint("ck_shop_settings_landline", Matches(["landline_phone"], ShopContactRules.LandlinePattern));
            table.HasCheckConstraint("ck_shop_settings_handles", Matches(Handles, ShopContactRules.HandlePattern));
        });

        builder.Property(settings => settings.MobilePhone).HasMaxLength(ShopContactRules.MaxLength);
        builder.Property(settings => settings.LandlinePhone).HasMaxLength(ShopContactRules.MaxLength);
        builder.Property(settings => settings.WhatsAppPhone).HasMaxLength(ShopContactRules.MaxLength);
        builder.Property(settings => settings.Telegram).HasMaxLength(ShopContactRules.MaxLength);
        builder.Property(settings => settings.Rubika).HasMaxLength(ShopContactRules.MaxLength);
        builder.Property(settings => settings.Eitaa).HasMaxLength(ShopContactRules.MaxLength);
        builder.Property(settings => settings.Bale).HasMaxLength(ShopContactRules.MaxLength);
        builder.Property(settings => settings.Instagram).HasMaxLength(ShopContactRules.MaxLength);

        builder.Property(settings => settings.Version).IsRowVersion();

        // The one row, every way empty until the shop admin fills them.
        builder.HasData(new
        {
            Id = ShopSettings.TheId,
            CreatedAt = new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero),
        });
    }

    /// <summary>Each column empty or in the shape; Postgres' <c>~</c> reads the same pattern as .NET's.</summary>
    private static string Matches(IEnumerable<string> columns, string pattern) =>
        string.Join(" AND ", columns.Select(column => $"({column} IS NULL OR {column} ~ '{pattern}')"));
}
