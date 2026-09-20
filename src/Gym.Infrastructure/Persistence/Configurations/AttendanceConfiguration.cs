using Gym.Application.Attendances;
using Gym.Domain.Attendances;
using Gym.Domain.Lockers;
using Gym.Domain.Members;
using Gym.Domain.Subscriptions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>attendances</c> table. The rules live in <see cref="Attendance"/>; the database repeats
/// every one it can check.
/// </summary>
/// <remarks>
/// The two partial unique indexes (BUSINESS_RULES.md §7) — one open attendance per member, one
/// per locker — are ordinary EF Core filtered indexes, unlike the subscriptions no-overlap rule,
/// which needed a hand-written exclusion constraint the ORM cannot express.
/// </remarks>
public sealed class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.ToTable("attendances", table =>
        {
            // BUSINESS_RULES.md §7: cancelling always closes the attendance at the same moment
            // (Attendance.Cancel sets both together), the same pairing SubscriptionConfiguration
            // enforces for CancelledAt and CancellationReason.
            table.HasCheckConstraint(
                "ck_attendances_cancellation",
                "cancelled_at IS NULL OR checked_out_at = cancelled_at");
        });

        // Restrict: a visit is a record that must never disappear with the member, subscription
        // or locker it references, the same reasoning SubscriptionConfiguration uses.
        builder.HasOne<Member>().WithMany().HasForeignKey(a => a.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Subscription>().WithMany().HasForeignKey(a => a.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Locker>().WithMany().HasForeignKey(a => a.LockerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.MemberId)
            .IsUnique()
            .HasFilter("checked_out_at IS NULL")
            .HasDatabaseName(AttendanceConstraints.OneOpenPerMember);

        builder.HasIndex(a => a.LockerId)
            .IsUnique()
            .HasFilter("checked_out_at IS NULL")
            .HasDatabaseName(AttendanceConstraints.OneOpenPerLocker);
    }
}
