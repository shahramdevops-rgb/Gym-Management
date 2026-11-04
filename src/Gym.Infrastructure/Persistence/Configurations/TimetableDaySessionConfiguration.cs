using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>The <c>timetable_day_sessions</c> table: the sessions of a date the trainer changed.</summary>
public sealed class TimetableDaySessionConfiguration : IEntityTypeConfiguration<TimetableDaySession>
{
    public void Configure(EntityTypeBuilder<TimetableDaySession> builder)
    {
        builder.ToTable("timetable_day_sessions", table => TimetableSessionColumns.AddChecks(table, "timetable_day_sessions"));

        // Made in C# (Entity), and added through the day's list, as TrainerPeriod's are.
        builder.Property(session => session.Id).ValueGeneratedNever();

        TimetableSessionColumns.Map(builder, session => session.Start, session => session.End);

        builder.HasIndex(session => new { session.DayId, session.Start, session.End }).IsUnique();
    }
}
