using Gym.Domain.Members;
using Gym.Domain.Plans;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>subscriptions</c> table. The rules live in <see cref="Subscription"/>; the database
/// repeats every one it can check.
/// </summary>
/// <remarks>
/// The rule that a member's subscriptions never overlap is an exclusion constraint, which EF
/// Core cannot express. It is written in SQL in the <c>AddSubscriptions</c> migration, under the
/// name <c>SubscriptionConstraints.NoOverlap</c>.
/// </remarks>
public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions", table =>
        {
            table.HasCheckConstraint("ck_subscriptions_dates", "end_date >= start_date");
            table.HasCheckConstraint(
                "ck_subscriptions_duration_days_range", $"duration_days BETWEEN 1 AND {Plan.MaxDurationDays}");
            table.HasCheckConstraint("ck_subscriptions_price_not_negative", "price >= 0");
            table.HasCheckConstraint(
                "ck_subscriptions_total_sessions_range",
                $"total_sessions IS NULL OR total_sessions BETWEEN 1 AND {Plan.MaxSessionCount}");

            // BUSINESS_RULES.md §4: used sessions never exceed the total (unlimited has none).
            table.HasCheckConstraint(
                "ck_subscriptions_used_sessions",
                "used_sessions >= 0 AND (total_sessions IS NULL OR used_sessions <= total_sessions)");
            table.HasCheckConstraint("ck_subscriptions_total_frozen_days", "total_frozen_days >= 0");

            // Cancelled means both a moment and a reason, never one without the other.
            table.HasCheckConstraint(
                "ck_subscriptions_cancellation",
                "(cancelled_at IS NULL) = (cancellation_reason IS NULL)");
        });

        builder.Property(s => s.PlanName).HasMaxLength(Plan.NameMaxLength).IsRequired();
        builder.Property(s => s.Price).HasPrecision(18, Plan.PriceDecimals);
        builder.Property(s => s.CancellationReason).HasMaxLength(Subscription.CancellationReasonMaxLength);

        // Restrict: members and plans are deactivated, never deleted, and a sale is a financial
        // record that must never disappear with them.
        builder.HasOne<Member>().WithMany().HasForeignKey(s => s.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Plan>().WithMany().HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict);

        // A member's subscriptions by date: the sale's calendar lookup and, later, the history.
        builder.HasIndex(s => new { s.MemberId, s.EndDate });

        builder.Ignore(s => s.IsUnlimited);
        builder.Ignore(s => s.RemainingSessions);

        builder.Property(s => s.Version).IsRowVersion();
    }
}
