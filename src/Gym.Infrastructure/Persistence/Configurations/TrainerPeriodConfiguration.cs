using Gym.Application.Trainers;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>The <c>trainer_periods</c> table: when each trainer was one (BUSINESS_RULES.md §15).</summary>
public sealed class TrainerPeriodConfiguration : IEntityTypeConfiguration<TrainerPeriod>
{
    public void Configure(EntityTypeBuilder<TrainerPeriod> builder)
    {
        builder.ToTable("trainer_periods", table =>
            table.HasCheckConstraint("ck_trainer_periods_ended_on_not_before_started_on", "ended_on IS NULL OR ended_on >= started_on"));

        // The id is made in C# (Entity). A period added to a trainer already in the database is
        // found through the trainer's list; with a key EF Core thought it generates, a set key
        // would read as "already saved" and the insert would go out as an update.
        builder.Property(period => period.Id).ValueGeneratedNever();

        // One open period per trainer: what makes "a trainer now" a single fact. Named, because the
        // foreign key's own index is on the same column and EF Core would merge two unnamed ones.
        builder.HasIndex(period => period.TrainerId, "OneOpenPeriod")
            .IsUnique()
            .HasFilter("ended_on IS NULL")
            .HasDatabaseName(TrainerConstraints.OneOpenPeriod);
    }
}
