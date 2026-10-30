using Gym.Domain.Timetables;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>The <c>timetable_days</c> table: the dates a trainer changed on their own (BUSINESS_RULES.md §15).</summary>
public sealed class TimetableDayConfiguration : IEntityTypeConfiguration<TimetableDay>
{
    public void Configure(EntityTypeBuilder<TimetableDay> builder)
    {
        builder.ToTable("timetable_days");

        builder.HasOne<Trainer>().WithMany().HasForeignKey(day => day.TrainerId).OnDelete(DeleteBehavior.Restrict);

        // One change per trainer and date.
        builder.HasIndex(day => new { day.TrainerId, day.Date }).IsUnique();

        // «برگرداندن به الگو» deletes the day, and its sessions with it.
        builder.HasMany(day => day.Sessions).WithOne().HasForeignKey(session => session.DayId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(day => day.IsClosed);

        builder.Property(day => day.Version).IsRowVersion();
    }
}
