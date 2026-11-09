using Gym.Application.Coaching;
using Gym.Domain.Coaching;
using Gym.Domain.Members;
using Gym.Domain.Trainers;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>The <c>trainer_requests</c> table: members asking trainers (BUSINESS_RULES.md §15).</summary>
public sealed class TrainerRequestConfiguration : IEntityTypeConfiguration<TrainerRequest>
{
    public void Configure(EntityTypeBuilder<TrainerRequest> builder)
    {
        builder.ToTable("trainer_requests", table =>
        {
            table.HasCheckConstraint(
                "ck_trainer_requests_status",
                "status IN ('Pending', 'Accepted', 'Declined', 'Cancelled', 'Replaced', 'TrainerStopped')");

            // Only the trainer's answer has a day and can be seen (task 15.4d). The answers given
            // before 15.4d have no day, and were marked seen by its migration.
            table.HasCheckConstraint(
                "ck_trainer_requests_answered_on_with_answer",
                "answered_on IS NULL OR status IN ('Accepted', 'Declined')");
            table.HasCheckConstraint(
                "ck_trainer_requests_answer_seen_at_with_answer",
                "answer_seen_at IS NULL OR status IN ('Accepted', 'Declined')");
        });

        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(20);

        // Members and trainers are deactivated or stopped, never deleted, and a request is history.
        builder.HasOne<Member>().WithMany().HasForeignKey(request => request.MemberId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Trainer>().WithMany().HasForeignKey(request => request.TrainerId).OnDelete(DeleteBehavior.Restrict);

        // One request waiting per member: picking another trainer replaces it first. Named, because
        // the foreign key's own index is on the same column and EF Core would merge two unnamed ones.
        builder.HasIndex(request => request.MemberId, "OnePendingRequest")
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName(CoachingConstraints.OnePendingRequest);

        // The trainer's list of requests waiting for them.
        builder.HasIndex(request => new { request.TrainerId, request.Status });

        builder.Property(request => request.Version).IsRowVersion();
    }
}
