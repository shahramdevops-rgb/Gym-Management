using Gym.Domain.Timetables;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>The <c>week_patterns</c> table: each trainer's weekly patterns, by the day each starts (BUSINESS_RULES.md §15).</summary>
public sealed class WeekPatternConfiguration : IEntityTypeConfiguration<WeekPattern>
{
    public void Configure(EntityTypeBuilder<WeekPattern> builder)
    {
        builder.ToTable("week_patterns");

        builder.HasOne<Trainer>().WithMany().HasForeignKey(pattern => pattern.TrainerId).OnDelete(DeleteBehavior.Restrict);

        // One pattern per trainer and start date; also the index the pattern in force is found by.
        builder.HasIndex(pattern => new { pattern.TrainerId, pattern.StartsOn }).IsUnique();

        // A pattern replaced by the trainer goes with its sessions.
        builder.HasMany(pattern => pattern.Sessions).WithOne().HasForeignKey(session => session.PatternId).OnDelete(DeleteBehavior.Cascade);

        builder.Property(pattern => pattern.Version).IsRowVersion();
    }
}
