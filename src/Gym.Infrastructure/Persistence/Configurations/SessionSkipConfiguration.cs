using Gym.Domain.Bookings;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>The <c>session_skips</c> table: the dates of a fixed schedule its student is out of.</summary>
public sealed class SessionSkipConfiguration : IEntityTypeConfiguration<SessionSkip>
{
    public void Configure(EntityTypeBuilder<SessionSkip> builder)
    {
        builder.ToTable("session_skips", table =>
        {
            TimetableSessionColumns.AddTimeChecks(table, "session_skips");
            table.HasCheckConstraint("ck_session_skips_booked_by", "booked_by IN ('Trainer', 'Member')");
        });

        TimetableSessionColumns.Map(builder, skip => skip.Start, skip => skip.End);
        builder.Property(skip => skip.By).HasColumnName("booked_by").HasConversion<string>().HasMaxLength(10);

        // Out of one date's session once.
        builder.HasIndex(skip => new { skip.ScheduleId, skip.Date, skip.Start, skip.End }).IsUnique();
    }
}
