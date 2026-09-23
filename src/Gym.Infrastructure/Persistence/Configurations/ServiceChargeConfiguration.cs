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
        });

        builder.Property(charge => charge.Amount).HasPrecision(18, ServiceCharge.AmountDecimals);
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

        // BUSINESS_RULES.md §7: one non-voided charge per visit per kind. A voided one is history
        // and does not stand in the way of the replacement charge the rule expects.
        builder.HasIndex(charge => new { charge.AttendanceId, charge.Kind })
            .IsUnique()
            .HasFilter("voided_at IS NULL")
            .HasDatabaseName(ServiceChargeConstraints.OneLivePerVisitAndKind);

        // The member's debt and its breakdown both filter by this column.
        builder.HasIndex(charge => charge.MemberId);

        builder.Property(charge => charge.Version).IsRowVersion();
    }
}
