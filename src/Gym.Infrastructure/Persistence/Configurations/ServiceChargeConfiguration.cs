using Gym.Application.ServiceCharges;
using Gym.Domain.Attendances;
using Gym.Domain.Members;
using Gym.Domain.ServiceCharges;
using Gym.Infrastructure.Identity;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>service_charges</c> table. The rules live in <see cref="ServiceCharge"/>; the database
/// repeats every one it can check.
/// </summary>
public sealed class ServiceChargeConfiguration : IEntityTypeConfiguration<ServiceCharge>
{
    public void Configure(EntityTypeBuilder<ServiceCharge> builder)
    {
        builder.ToTable("service_charges", table =>
        {
            table.HasCheckConstraint("ck_service_charges_amount_positive", "amount > 0");

            // Voided means a moment, a reason and a user, never one without the others — the same
            // pairing SubscriptionConfiguration enforces for a cancellation.
            table.HasCheckConstraint(
                "ck_service_charges_void",
                "(voided_at IS NULL) = (void_reason IS NULL) AND (voided_at IS NULL) = (voided_by_user_id IS NULL)");

            // BUSINESS_RULES.md §7 Miscellaneous sale: a sale has its name, quantity and unit price,
            // all three, and its amount is exactly what they make; هوازی has none of them. Rows
            // written before the kind existed are all هوازی with the three columns null, so they
            // pass as they are.
            table.HasCheckConstraint(
                "ck_service_charges_miscellaneous",
                "(kind = 'Miscellaneous') = (description IS NOT NULL) "
                + "AND (description IS NULL) = (quantity IS NULL) "
                + "AND (description IS NULL) = (unit_price IS NULL) "
                + $"AND (quantity IS NULL OR (quantity BETWEEN 1 AND {ServiceCharge.MaxQuantity} "
                + "AND unit_price > 0 AND amount = unit_price * quantity))");
        });

        builder.Property(charge => charge.Amount).HasPrecision(18, ServiceCharge.AmountDecimals);
        builder.Property(charge => charge.UnitPrice).HasPrecision(18, ServiceCharge.AmountDecimals);
        builder.Property(charge => charge.Description).HasMaxLength(ServiceCharge.DescriptionMaxLength);
        builder.Property(charge => charge.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(charge => charge.VoidReason).HasMaxLength(ServiceCharge.VoidReasonMaxLength);

        // Restrict: a charge is a financial record and must never disappear with the visit or the
        // member it belongs to, the same reasoning PaymentConfiguration uses.
        builder.HasOne<Member>().WithMany().HasForeignKey(charge => charge.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Attendance>().WithMany().HasForeignKey(charge => charge.AttendanceId).OnDelete(DeleteBehavior.Restrict);

        // Users are deactivated, never deleted, so a user who recorded or voided a charge can
        // never disappear.
        builder.HasOne<User>().WithMany().HasForeignKey(charge => charge.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(charge => charge.VoidedByUserId).OnDelete(DeleteBehavior.Restrict);

        // BUSINESS_RULES.md §7: one non-voided هوازی per visit. A voided one is history and does
        // not stand in the way of the replacement charge the rule expects. A visit may have any
        // number of miscellaneous sales, so they are outside the filter.
        builder.HasIndex(charge => new { charge.AttendanceId, charge.Kind })
            .IsUnique()
            .HasFilter("voided_at IS NULL AND kind = 'Cardio'")
            .HasDatabaseName(ServiceChargeConstraints.OneLivePerVisitAndKind);

        // The member's debt and its breakdown both filter by this column.
        builder.HasIndex(charge => charge.MemberId);

        // Not a rule, only speed: the gym's هوازی history reads a range of days, newest first
        // (BUSINESS_RULES.md §12 History).
        builder.HasIndex(charge => charge.ChargedOn);

        builder.Property(charge => charge.Version).IsRowVersion();
    }
}
