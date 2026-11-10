using Gym.Domain.Coaching;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>coaching_periods</c> table: each student's دورهٔ مربی‌گری with their trainer and its
/// renewals (BUSINESS_RULES.md §15, 15.7h). Two periods of a link never share a day, held by the
/// exclusion constraint <c>ex_coaching_periods_link_id_dates</c>, written by hand in the migration
/// <c>AddCoachingPeriods</c>: EF Core has no way to declare one.
/// </summary>
public sealed class CoachingPeriodConfiguration : IEntityTypeConfiguration<CoachingPeriod>
{
    public void Configure(EntityTypeBuilder<CoachingPeriod> builder)
    {
        builder.ToTable("coaching_periods", table =>
        {
            table.HasCheckConstraint("ck_coaching_periods_ends_on_not_before_starts_on", "ends_on >= starts_on");

            // One period is at most the timetable's 12 weeks (CoachingPeriod.MaxDays).
            table.HasCheckConstraint("ck_coaching_periods_at_most_84_days", "ends_on - starts_on < 84");
        });

        // The id is made in C# (Entity). A period added to a link already in the database is found
        // through the link's list; with a key EF Core thought it generates, a set key would read as
        // "already saved" and the insert would go out as an update.
        builder.Property(period => period.Id).ValueGeneratedNever();

        // A link's periods, latest first, for its last day.
        builder.HasIndex(period => new { period.LinkId, period.EndsOn });
    }
}
