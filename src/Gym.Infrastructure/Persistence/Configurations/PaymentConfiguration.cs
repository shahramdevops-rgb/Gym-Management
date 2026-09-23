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

        // Users are deactivated, never deleted, so a user who received payments can never disappear.
        builder.HasOne<User>().WithMany().HasForeignKey(payment => payment.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);

        // A payment history and a net-paid calculation filter by one of these two columns.
        builder.HasIndex(payment => payment.SubscriptionId);
        builder.HasIndex(payment => payment.ServiceChargeId);
    }
}
