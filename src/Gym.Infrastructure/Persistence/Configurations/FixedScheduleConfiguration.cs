using Gym.Domain.Bookings;
using Gym.Domain.Coaching;
using Gym.Domain.Members;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>fixed_schedules</c> table: each student's «برنامهٔ ثابت» (BUSINESS_RULES.md §15). One per
/// link at a time is held by the exclusion constraint <c>ex_fixed_schedules_link_id_dates</c>,
/// written by hand in the migration <c>AddFixedSchedules</c>: EF Core has no way to declare one.
/// </summary>
public sealed class FixedScheduleConfiguration : IEntityTypeConfiguration<FixedSchedule>
{
    public void Configure(EntityTypeBuilder<FixedSchedule> builder)
    {
        builder.ToTable("fixed_schedules", table =>
            table.HasCheckConstraint("ck_fixed_schedules_ends_on_not_before_starts_on", "ends_on >= starts_on"));

        builder.HasOne<Trainer>().WithMany().HasForeignKey(schedule => schedule.TrainerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Member>().WithMany().HasForeignKey(schedule => schedule.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<TrainerLink>().WithMany().HasForeignKey(schedule => schedule.LinkId).OnDelete(DeleteBehavior.Restrict);

        // A trainer's places over a range of dates, and a member's.
        builder.HasIndex(schedule => new { schedule.TrainerId, schedule.EndsOn });
        builder.HasIndex(schedule => new { schedule.MemberId, schedule.EndsOn });

        // A schedule not started yet is replaced by deleting it: its weekly sessions and its
        // skipped dates go with it.
        builder.HasMany(schedule => schedule.Slots).WithOne().HasForeignKey(slot => slot.ScheduleId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany<SessionSkip>().WithOne().HasForeignKey(skip => skip.ScheduleId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(schedule => schedule.Version).IsRowVersion();
    }
}
