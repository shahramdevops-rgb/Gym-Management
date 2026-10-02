using Gym.Domain.Attendances;
using Gym.Domain.Cafe;
using Gym.Domain.Members;
using Gym.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>cafe_orders</c> table. The rules live in <see cref="CafeOrder"/>; the database repeats
/// every one it can check.
/// </summary>
public sealed class CafeOrderConfiguration : IEntityTypeConfiguration<CafeOrder>
{
    public void Configure(EntityTypeBuilder<CafeOrder> builder)
    {
        builder.ToTable("cafe_orders", table =>
        {
            table.HasCheckConstraint("ck_cafe_orders_total_not_negative", "total_amount >= 0");

            // Cancelled means a moment, a reason and a user, never one without the others — the
            // same pairing ServiceChargeConfiguration enforces for a void.
            table.HasCheckConstraint(
                "ck_cafe_orders_cancel",
                "(cancelled_at IS NULL) = (cancel_reason IS NULL) AND (cancelled_at IS NULL) = (cancelled_by_user_id IS NULL)");

            // No check that an order on a visit names a member: since roadmap 6.5.11 an order on a
            // guest's visit names none (BUSINESS_RULES.md §7 Guest visit), and a check sees only
            // this row, so it cannot tell a guest's visit from a member's. CafeOrder.Create and
            // CreateForGuestVisit keep the rule, with the handler checking the visit.
        });

        builder.Property(order => order.TotalAmount).HasPrecision(18, 2);
        builder.Property(order => order.CancelReason).HasMaxLength(CafeOrder.CancelReasonMaxLength);

        // Restrict: an order is a financial record and must never disappear with the member it
        // belongs to, the same reasoning PaymentConfiguration uses. Null is a walk-in customer.
        builder.HasOne<Member>().WithMany().HasForeignKey(order => order.MemberId).OnDelete(DeleteBehavior.Restrict);

        // Restrict, like the member: attendances are never deleted (a mistaken one is cancelled),
        // and an order must not lose the visit it was bought during. Null is an order from the till.
        builder.HasOne<Attendance>().WithMany().HasForeignKey(order => order.AttendanceId).OnDelete(DeleteBehavior.Restrict);

        // Users are deactivated, never deleted, so whoever rang up or cancelled an order stays.
        builder.HasOne<User>().WithMany().HasForeignKey(order => order.PlacedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(order => order.CancelledByUserId).OnDelete(DeleteBehavior.Restrict);

        // Cascade, and the only cascade in the schema: a line has no meaning without its order and
        // no money of its own — the payments hang off the order. Deleting an order is not something
        // the application ever does, so this is about the model being honest, not about a feature.
        builder.HasMany(order => order.Items)
            .WithOne()
            .HasForeignKey(item => item.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(order => order.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        // The member's debt and their purchase history both filter by this column; reports filter
        // by the date (BUSINESS_RULES.md §12).
        builder.HasIndex(order => order.MemberId);
        builder.HasIndex(order => order.OrderedOn);

        // The board and check-out both ask "what did this visit buy?".
        builder.HasIndex(order => order.AttendanceId);

        builder.Ignore(order => order.IsCancelled);

        builder.Property(order => order.Version).IsRowVersion();
    }
}

/// <summary>The <c>cafe_order_items</c> table: the snapshot lines of one order.</summary>
public sealed class CafeOrderItemConfiguration : IEntityTypeConfiguration<CafeOrderItem>
{
    public void Configure(EntityTypeBuilder<CafeOrderItem> builder)
    {
        builder.ToTable("cafe_order_items", table =>
        {
            table.HasCheckConstraint(
                "ck_cafe_order_items_quantity_range", $"quantity BETWEEN 1 AND {CafeOrderItem.MaxQuantity}");
            table.HasCheckConstraint("ck_cafe_order_items_unit_price_not_negative", "unit_price >= 0");

            // The stored line total is what the customer was charged, so the database checks it is
            // still the product of the two columns beside it rather than trusting the writer.
            table.HasCheckConstraint("ck_cafe_order_items_line_total", "line_total = unit_price * quantity");
        });

        builder.Property(item => item.ProductName).HasMaxLength(Product.NameMaxLength).IsRequired();
        builder.Property(item => item.UnitPrice).HasPrecision(18, Product.PriceDecimals);
        builder.Property(item => item.LineTotal).HasPrecision(18, Product.PriceDecimals);

        // Restrict: the line names the product it sold, for the "top cafe products" report
        // (BUSINESS_RULES.md §12). This is the reason a product is switched off and never deleted.
        builder.HasOne<Product>().WithMany().HasForeignKey(item => item.ProductId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(item => item.OrderId);
        builder.HasIndex(item => item.ProductId);
    }
}
