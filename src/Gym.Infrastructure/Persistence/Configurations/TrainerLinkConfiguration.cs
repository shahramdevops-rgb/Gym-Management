using Gym.Application.Coaching;
using Gym.Domain.Coaching;
using Gym.Domain.Members;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>The <c>trainer_links</c> table: who trained whom, and when (BUSINESS_RULES.md §15).</summary>
public sealed class TrainerLinkConfiguration : IEntityTypeConfiguration<TrainerLink>
{
    public void Configure(EntityTypeBuilder<TrainerLink> builder)
    {
        builder.ToTable("trainer_links", table =>
        {
            table.HasCheckConstraint("ck_trainer_links_ended_on_not_before_started_on", "ended_on IS NULL OR ended_on >= started_on");

            // Ended means a reason, and open means none: the two columns say one thing.
            table.HasCheckConstraint("ck_trainer_links_end_reason_with_ended_on", "(ended_on IS NULL) = (end_reason IS NULL)");
            table.HasCheckConstraint(
                "ck_trainer_links_end_reason",
                "end_reason IS NULL OR end_reason IN ('ByMember', 'ByTrainer', 'NewTrainer', 'TrainerStopped')");

            // Only an end can be seen (task 15.4d).
            table.HasCheckConstraint("ck_trainer_links_end_seen_at_with_ended_on", "end_seen_at IS NULL OR ended_on IS NOT NULL");
        });

        builder.Property(link => link.EndReason).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Member>().WithMany().HasForeignKey(link => link.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Trainer>().WithMany().HasForeignKey(link => link.TrainerId).OnDelete(DeleteBehavior.Restrict);

        // One link per accepted request.
        builder.HasOne<TrainerRequest>().WithMany().HasForeignKey(link => link.RequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(link => link.RequestId).IsUnique();

        // Each member has at most one trainer at a time (BUSINESS_RULES.md §15).
        builder.HasIndex(link => link.MemberId, "OneOpenLink")
            .IsUnique()
            .HasFilter("ended_on IS NULL")
            .HasDatabaseName(CoachingConstraints.OneOpenLink);

        // The trainer's list of students.
        builder.HasIndex(link => new { link.TrainerId, link.EndedOn });

        // Links are ended, never deleted; the cascade only says a period means nothing alone.
        builder.HasMany(link => link.Periods)
            .WithOne()
            .HasForeignKey(period => period.LinkId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(link => link.Periods).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(link => link.Version).IsRowVersion();
    }
}
