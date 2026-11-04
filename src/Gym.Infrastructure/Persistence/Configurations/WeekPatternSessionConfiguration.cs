using Gym.Domain.Timetables;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>The <c>week_pattern_sessions</c> table: the sessions of each day of a weekly pattern.</summary>
public sealed class WeekPatternSessionConfiguration : IEntityTypeConfiguration<WeekPatternSession>
{
    public void Configure(EntityTypeBuilder<WeekPatternSession> builder)
    {
        builder.ToTable("week_pattern_sessions", table =>
        {
            // DayOfWeek's numbers: 0 is Sunday, 6 Saturday.
            table.HasCheckConstraint("ck_week_pattern_sessions_day", "day BETWEEN 0 AND 6");
            TimetableSessionColumns.AddChecks(table, "week_pattern_sessions");
        });

        // Made in C# (Entity), and added through the pattern's list, as TrainerPeriod's are.
        builder.Property(session => session.Id).ValueGeneratedNever();

        TimetableSessionColumns.Map(builder, session => session.Start, session => session.End);

        // Two sessions of one day never start and end together (BUSINESS_RULES.md §15).
        builder.HasIndex(session => new { session.PatternId, session.Day, session.Start, session.End }).IsUnique();
    }
}
