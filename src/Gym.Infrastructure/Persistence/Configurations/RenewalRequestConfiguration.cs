using Gym.Application.Coaching;
using Gym.Domain.Coaching;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

/// <summary>
/// The <c>renewal_requests</c> table: a student asking their trainer to renew the coaching period,
/// and the answer (BUSINESS_RULES.md §15 <i>The coaching period</i>, task 15.7o).
/// </summary>
public sealed class RenewalRequestConfiguration : IEntityTypeConfiguration<RenewalRequest>
{
    public void Configure(EntityTypeBuilder<RenewalRequest> builder)
    {
        builder.ToTable("renewal_requests", table =>
        {
            table.HasCheckConstraint(
                "ck_renewal_requests_status",
                "status IN ('Pending', 'Renewed', 'Declined', 'Cancelled', 'LinkEnded')");

            // Only the trainer's answer has a day, and it has one always.
            table.HasCheckConstraint(
                "ck_renewal_requests_answered_on_with_answer",
                "(answered_on IS NOT NULL) = (status IN ('Renewed', 'Declined'))");
            table.HasCheckConstraint(
                "ck_renewal_requests_answered_on_not_before_requested_on",
                "answered_on IS NULL OR answered_on >= requested_on");
        });

        builder.Property(request => request.Status).HasConversion<string>().HasMaxLength(10);

        // Links are ended, never deleted, and a request is history.
        builder.HasOne<TrainerLink>().WithMany().HasForeignKey(request => request.LinkId).OnDelete(DeleteBehavior.Restrict);

        // One request waiting per link. Named, because the foreign key's own index is on the same
        // column and EF Core would merge two unnamed ones.
        builder.HasIndex(request => request.LinkId, "OnePendingRenewal")
            .IsUnique()
            .HasFilter("status = 'Pending'")
            .HasDatabaseName(CoachingConstraints.OnePendingRenewal);

        // «مربی من» reads the link's latest request, whatever became of it.
        builder.HasIndex(request => new { request.LinkId, request.CreatedAt });

        builder.Property(request => request.Version).IsRowVersion();
    }
}
