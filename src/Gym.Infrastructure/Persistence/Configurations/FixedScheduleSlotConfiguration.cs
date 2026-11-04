using Gym.Domain.Bookings;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>The <c>fixed_schedule_slots</c> table: the weekly sessions of a fixed schedule.</summary>
public sealed class FixedScheduleSlotConfiguration : IEntityTypeConfiguration<FixedScheduleSlot>
{
    public void Configure(EntityTypeBuilder<FixedScheduleSlot> builder)
    {
        builder.ToTable("fixed_schedule_slots", table =>
        {
            // DayOfWeek's numbers: 0 is Sunday, 6 Saturday.
            table.HasCheckConstraint("ck_fixed_schedule_slots_day", "day BETWEEN 0 AND 6");
            TimetableSessionColumns.AddTimeChecks(table, "fixed_schedule_slots");
        });

        // Made in C# (Entity), and added through the schedule's list, as WeekPatternSession's are.
        builder.Property(slot => slot.Id).ValueGeneratedNever();

        TimetableSessionColumns.Map(builder, slot => slot.Start, slot => slot.End);

        builder.HasIndex(slot => new { slot.ScheduleId, slot.Day, slot.Start, slot.End }).IsUnique();
    }
}
