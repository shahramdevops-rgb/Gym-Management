using Gym.Domain.Pricing;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>price_lists</c> table: one row, seeded by the migration with both prices empty
/// (BUSINESS_RULES.md §3 <i>Prices</i>). The rules live in <see cref="PriceList"/>; the database
/// repeats every one it can check.
/// </summary>
public sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.ToTable("price_lists", table =>
        {
            // One row, ever: the id is fixed, so a second row cannot be inserted even by hand.
            table.HasCheckConstraint("ck_price_lists_single_row", $"id = '{PriceList.TheId}'");
            table.HasCheckConstraint(
                "ck_price_lists_prices_not_negative",
                "(session_price IS NULL OR session_price >= 0) AND (single_visit_price IS NULL OR single_visit_price >= 0)");
        });

        // numeric(18,2): exact decimal arithmetic, never float (CLAUDE.md).
        builder.Property(prices => prices.SessionPrice).HasPrecision(18, PriceList.PriceDecimals);
        builder.Property(prices => prices.SingleVisitPrice).HasPrecision(18, PriceList.PriceDecimals);

        builder.Property(prices => prices.Version).IsRowVersion();

        // An anonymous object, because every setter on the entity is private (the same as the
        // lockers). Both prices start empty: they are the gym's own numbers (§3).
        builder.HasData(new
        {
            Id = PriceList.TheId,
            CreatedAt = new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
        });
    }
}
