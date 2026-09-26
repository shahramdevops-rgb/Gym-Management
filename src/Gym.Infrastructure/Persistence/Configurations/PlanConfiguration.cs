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

            // BUSINESS_RULES.md §3: the single-session plan is always one day and one session. The
            // entity refuses anything else; this makes it true of the table as well.
            table.HasCheckConstraint(
                "ck_plans_single_session_shape",
                $"kind <> '{nameof(PlanKind.SingleSession)}' OR (duration_days = 1 AND session_count = 1)");
        });

        builder.Property(plan => plan.Name).HasMaxLength(Plan.NameMaxLength).IsRequired();
        builder.Property(plan => plan.NormalizedName).HasMaxLength(Plan.NameMaxLength).IsRequired();

        // numeric(18,2): exact decimal arithmetic, never float (CLAUDE.md).
        builder.Property(plan => plan.Price).HasPrecision(18, Plan.PriceDecimals);

        // Stored as text like every other enum here: 'SingleSession' in a check constraint or a
        // psql session says what 1 would not.
        builder.Property(plan => plan.Kind).HasConversion<string>().HasMaxLength(20);

        // Unique among all plans, inactive ones included, in normalized form (task 3.1).
        builder.HasIndex(plan => plan.NormalizedName)
            .IsUnique()
            .HasDatabaseName(PlanConstraints.UniqueName);

        // BUSINESS_RULES.md §3: at most one single-session plan. Unique on the kind column but
        // filtered to that one value, so memberships are unaffected.
        builder.HasIndex(plan => plan.Kind)
            .IsUnique()
            .HasFilter($"kind = '{nameof(PlanKind.SingleSession)}'")
            .HasDatabaseName(PlanConstraints.UniqueSingleSession);

        builder.Ignore(plan => plan.IsUnlimited);
        builder.Ignore(plan => plan.IsSingleSession);

        builder.Property(plan => plan.Version).IsRowVersion();
    }
}
