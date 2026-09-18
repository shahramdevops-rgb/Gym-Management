using Gym.Application.Plans;
using Gym.Domain.Plans;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>plans</c> table. The rules live in <see cref="Plan"/>; the database repeats every one
/// it can check, so a bug or a hand-written SQL statement cannot store a plan the app would reject.
/// </summary>
public sealed class PlanConfiguration : IEntityTypeConfiguration<Plan>
{
    public void Configure(EntityTypeBuilder<Plan> builder)
    {
        builder.ToTable("plans", table =>
        {
            table.HasCheckConstraint("ck_plans_name_not_blank", "btrim(name) <> ''");
            table.HasCheckConstraint(
                "ck_plans_duration_days_range", $"duration_days BETWEEN 1 AND {Plan.MaxDurationDays}");

            // NULL means unlimited; otherwise at least one session.
            table.HasCheckConstraint(
                "ck_plans_session_count_range",
                $"session_count IS NULL OR session_count BETWEEN 1 AND {Plan.MaxSessionCount}");
            table.HasCheckConstraint("ck_plans_price_not_negative", "price >= 0");
        });

        builder.Property(plan => plan.Name).HasMaxLength(Plan.NameMaxLength).IsRequired();
        builder.Property(plan => plan.NormalizedName).HasMaxLength(Plan.NameMaxLength).IsRequired();

        // numeric(18,2): exact decimal arithmetic, never float (CLAUDE.md).
        builder.Property(plan => plan.Price).HasPrecision(18, Plan.PriceDecimals);

        // Unique among all plans, inactive ones included, in normalized form (task 3.1).
        builder.HasIndex(plan => plan.NormalizedName)
            .IsUnique()
            .HasDatabaseName(PlanConstraints.UniqueName);

        builder.Ignore(plan => plan.IsUnlimited);

        builder.Property(plan => plan.Version).IsRowVersion();
    }
}
