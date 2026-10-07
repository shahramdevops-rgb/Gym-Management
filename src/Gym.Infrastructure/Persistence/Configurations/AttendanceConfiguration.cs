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
/// The three partial unique indexes (BUSINESS_RULES.md §6, §7) — one open attendance per member,
/// one per locker, one per reserve place — are ordinary EF Core filtered indexes, unlike the subscriptions no-overlap rule,
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

            // The nightly job's own close (Attendance.AutoClose), the same pairing as cancellation.
            table.HasCheckConstraint(
                "ck_attendances_auto_close",
                "auto_closed_at IS NULL OR checked_out_at = auto_closed_at");

            // An attendance closes for exactly one reason: the member checking out, cancelling,
            // or the nightly job. Cancel and AutoClose each require the row to still be open
            // before they run, so the application can never set both — this is the database's
            // own backstop for that invariant.
            table.HasCheckConstraint(
                "ck_attendances_one_close_reason",
                "cancelled_at IS NULL OR auto_closed_at IS NULL");

            // BUSINESS_RULES.md §6 Reserve places: at most Attendance.ReservePlaceCount of them.
            table.HasCheckConstraint(
                AttendanceConstraints.ReserveSlotRange,
                $"reserve_slot BETWEEN 1 AND {Attendance.ReservePlaceCount}");

            // Every open visit holds exactly one of a locker and a reserve place. Only open ones:
            // visits closed before roadmap 6.5.5 may hold neither, and a closed visit keeps the
            // place it held, which is history rather than occupancy.
            table.HasCheckConstraint(
                AttendanceConstraints.OpenHoldsOnePlace,
                "checked_out_at IS NOT NULL OR ((locker_id IS NULL) <> (reserve_slot IS NULL))");

            // BUSINESS_RULES.md §7 Guest visit: a visit is a member's (with the subscription a
            // session came from) or a guest's (a name and nothing else), never both and never
            // neither. Every visit before roadmap 6.5.11 is a member's and passes all three.
            // §7 Cardio-only visit: such a visit needs no plan (roadmap 6.5.35), so a member's
            // visit may lack a subscription only when it is cardio-only. Every earlier visit had
            // one exactly when it had a member, which passes.
            table.HasCheckConstraint(
                AttendanceConstraints.MemberOrGuest,
                "(member_id IS NULL) <> (guest_name IS NULL)");
            table.HasCheckConstraint(
                AttendanceConstraints.SubscriptionWithMember,
                "(member_id IS NULL AND subscription_id IS NULL) OR (member_id IS NOT NULL AND (subscription_id IS NOT NULL OR is_cardio_only))");
            table.HasCheckConstraint(
                AttendanceConstraints.GuestNameNotBlank,
                "guest_name IS NULL OR btrim(guest_name) <> ''");

            // BUSINESS_RULES.md §7 Cardio-only visit: a member's visit that consumed no session from
            // the plan it names. A guest has no plan, so a guest's visit is never one. The column's
            // migration defaults every earlier visit to false, which passes.
            table.HasCheckConstraint(
                AttendanceConstraints.CardioOnlyIsMembers,
                "NOT is_cardio_only OR member_id IS NOT NULL");
        });

        builder.Property(a => a.GuestName).HasMaxLength(Attendance.GuestNameMaxLength);
        builder.Ignore(a => a.IsGuest);

        // The number is internal bookkeeping, never shown, and never above 15: smallint says so.
        builder.Property(a => a.ReserveSlot).HasColumnType("smallint");

        builder.Property(a => a.Version).IsRowVersion();

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

        builder.HasIndex(a => a.ReserveSlot)
            .IsUnique()
            .HasFilter("checked_out_at IS NULL")
            .HasDatabaseName(AttendanceConstraints.OneOpenPerReserveSlot);

        // Not a rule, only speed: the desk's charts read visits by a range of check-in moments (today
        // by hour, and a locker's use over a period). Nothing reacts to its name.
        builder.HasIndex(a => a.CheckedInAt);

        // Speed only, like the one above: the member list is sorted by each member's latest visit
        // that was not cancelled (BUSINESS_RULES.md §2), which reads this index per member.
        builder.HasIndex(a => new { a.MemberId, a.CheckedInAt })
            .HasFilter("cancelled_at IS NULL");
    }
}
