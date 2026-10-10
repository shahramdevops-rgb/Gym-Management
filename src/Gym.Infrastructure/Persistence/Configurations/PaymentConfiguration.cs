using Gym.Domain.Cafe;
using Gym.Domain.Payments;
using Gym.Domain.ServiceCharges;
using Gym.Domain.Subscriptions;
using Gym.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>payments</c> table. The rules live in <see cref="Payment"/>; the database repeats
/// every one it can check.
/// </summary>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", table =>
        {
            // BUSINESS_RULES.md §5: a payment belongs to exactly one of a subscription, a cafe
            // order or a service charge. num_nonnulls ships with Postgres; no cafe_order_id is
            // ever set before Phase 7 adds cafe orders, but the constraint holds for all three.
            table.HasCheckConstraint(
                "ck_payments_one_target",
                "num_nonnulls(subscription_id, cafe_order_id, service_charge_id) = 1");

            table.HasCheckConstraint("ck_payments_amount_positive", "amount > 0");

            // A refund's reason explains what is being undone; a payment has none.
            table.HasCheckConstraint("ck_payments_refund_reason", "kind = 'Payment' OR reason IS NOT NULL");

            // A refund is never part of a «تسویه یکجا» (BUSINESS_RULES.md §5): each row of one is
            // refunded on its own.
            table.HasCheckConstraint("ck_payments_settlement_payment_only", "settlement_id IS NULL OR kind = 'Payment'");
        });

        builder.Property(payment => payment.Amount).HasPrecision(18, Payment.AmountDecimals);
        builder.Property(payment => payment.Kind).HasConversion<string>().HasMaxLength(10);
        builder.Property(payment => payment.Method).HasConversion<string>().HasMaxLength(20);
        builder.Property(payment => payment.ReferenceNumber).HasMaxLength(Payment.ReferenceNumberMaxLength);
        builder.Property(payment => payment.Reason).HasMaxLength(Payment.ReasonMaxLength);

        // Restrict: a payment is a financial record and must never disappear with the thing it
        // paid for.
        builder.HasOne<Subscription>().WithMany().HasForeignKey(payment => payment.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ServiceCharge>().WithMany().HasForeignKey(payment => payment.ServiceChargeId).OnDelete(DeleteBehavior.Restrict);

        // Missing until task 11.3: the cafe orders of Phase 7 came after this configuration, and
        // only the code checked that a cafe payment's order exists. Production had no payment
        // without its order when it was added, so the migration refused no row.
        builder.HasOne<CafeOrder>().WithMany().HasForeignKey(payment => payment.CafeOrderId).OnDelete(DeleteBehavior.Restrict);

        // Users are deactivated, never deleted, so a user who received payments can never disappear.
        builder.HasOne<User>().WithMany().HasForeignKey(payment => payment.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);

        // A payment history and a net-paid calculation filter by one of these three columns.
        // CafeOrderId's came only in task 11.3: without it, every member's debt read the whole
        // payments table once per cafe order, 18 seconds with three years of data
        // (docs/performance-review.md).
        builder.HasIndex(payment => payment.SubscriptionId);
        builder.HasIndex(payment => payment.ServiceChargeId);
        builder.HasIndex(payment => payment.CafeOrderId);

        // Not a rule, only speed: the gym's payment history reads a range of PaidAt moments, newest
        // first (BUSINESS_RULES.md §12 History), and the revenue reports will read the same range.
        builder.HasIndex(payment => payment.PaidAt);

        // The history sums each settlement's rows for its header (§12 History). Most payments are
        // taken on their own, so only the rows that belong to a settlement are indexed.
        builder.HasIndex(payment => payment.SettlementId).HasFilter("settlement_id IS NOT NULL");
    }
}
