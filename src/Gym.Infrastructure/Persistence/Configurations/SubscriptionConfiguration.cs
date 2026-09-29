using Gym.Domain.Members;
using Gym.Domain.Pricing;
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
/// name <c>SubscriptionConstraints.NoOverlap</c>, and recreated by <c>AddSingleSessionPlan</c> with
/// <c>AND NOT is_single_session</c> in its condition (BUSINESS_RULES.md §4): a single visit may
/// share a date with a membership, while two memberships still may not.
/// </remarks>
public sealed class SubscriptionConfiguration : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("subscriptions", table =>
        {
            table.HasCheckConstraint("ck_subscriptions_dates", "end_date >= start_date");
            table.HasCheckConstraint("ck_subscriptions_price_not_negative", "price >= 0");

            // BUSINESS_RULES.md §3: a plan has 5 to 140 sessions; a single visit has exactly 1 (its
            // own constraint below).
            table.HasCheckConstraint(
                "ck_subscriptions_total_sessions_range",
                $"is_single_session OR total_sessions BETWEEN {Subscription.MinSessionCount} AND {Subscription.MaxSessionCount}");

            // BUSINESS_RULES.md §3 (task 6.5.18): a plan lasts the days its sessions give. A count
            // above the table matches no row and the CASE is NULL, which a check lets through; the
            // sessions range above is what refuses it.
            table.HasCheckConstraint(
                "ck_subscriptions_duration_days_for_sessions",
                $"is_single_session OR duration_days = {DurationDaysSql()}");

            // BUSINESS_RULES.md §4: used sessions never exceed the total.
            table.HasCheckConstraint(
                "ck_subscriptions_used_sessions",
                "used_sessions >= 0 AND used_sessions <= total_sessions");
            table.HasCheckConstraint("ck_subscriptions_total_frozen_days", "total_frozen_days >= 0");

            // Cancelled means both a moment and a reason, never one without the other.
            table.HasCheckConstraint(
                "ck_subscriptions_cancellation",
                "(cancelled_at IS NULL) = (cancellation_reason IS NULL)");

            // BUSINESS_RULES.md §4: a single visit is one day and one session. The flag is what the
            // no-overlap constraint and the scheduling rules read, so a row that carries it while
            // describing something else would quietly opt a real membership out of the calendar.
            table.HasCheckConstraint(
                "ck_subscriptions_single_session_shape",
                "NOT is_single_session OR (duration_days = 1 AND total_sessions = 1)");
        });

        builder.Property(s => s.Price).HasPrecision(18, PriceList.PriceDecimals);
        builder.Property(s => s.CancellationReason).HasMaxLength(Subscription.CancellationReasonMaxLength);

        // Restrict: members are deactivated, never deleted, and a sale is a financial record that
        // must never disappear with them.
        builder.HasOne<Member>().WithMany().HasForeignKey(s => s.MemberId).OnDelete(DeleteBehavior.Restrict);

        // A member's subscriptions by date: the sale's calendar lookup and, later, the history.
        builder.HasIndex(s => new { s.MemberId, s.EndDate });

        builder.Ignore(s => s.RemainingSessions);

        builder.Property(s => s.Version).IsRowVersion();
    }

    /// <summary>
    /// <see cref="Subscription.DurationTable"/> as SQL:
    /// <c>CASE WHEN total_sessions &lt;= 10 THEN 30 WHEN … END</c>. Written from the table, so the
    /// database and the entity cannot disagree about a plan's days.
    /// </summary>
    private static string DurationDaysSql()
    {
        var rows = Subscription.DurationTable.Select(row => $"WHEN total_sessions <= {row.MaxSessions} THEN {row.Days}");

        return $"CASE {string.Join(' ', rows)} END";
    }
}
